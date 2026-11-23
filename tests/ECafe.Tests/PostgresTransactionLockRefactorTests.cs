using System.Data.Common;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.ContentLocalization;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.Localization;
using ECafe.Infrastructure.Repositories.Reservation;
using ECafe.Infrastructure.Repositories.ReservationRefund;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task SqlLockRefactorPreservesSqlParametersNamespacesAndSortedStockRows()
    {
        await using var fixture = await Database.CreateAsync();
        var commands = new LockCommandCapture();
        await using var db = fixture.Context(commands);
        db.Tables.AddRange(new Table { Id = 1005, RestaurantId = 1001, Capacity = 2, TableNo = 5 },
            new Table { Id = 1002, RestaurantId = 1001, Capacity = 2, TableNo = 2 });
        await db.SaveChangesAsync();
        commands.Items.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await new UserRefreshTokenRepository(db).AcquireUserLockAsync(1001);
        await new ReservationRepository(db).AcquireCustomerReservationLockAsync(1001, 1001);
        await new TableRepository(db).AcquireReservationLockAsync(1001, 1005);
        await new RestaurantRepository(db).AcquireScheduleLockAsync(1001);
        await new RestaurantScheduleRepository(db).AcquireLocksAsync(1001, default);
        await new ReservationRefundRepository(db).AcquireRefundLockAsync(55);
        await new TranslationRepository(db).AcquireLockAsync(default);
        var store = Mock.Of<IContentTranslationStore>(value => value.Type == ContentEntityType.Category);
        await new ContentTranslationRepository(db, [store]).AcquireLockAsync(ContentEntityType.Category, 77, default);
        await new InventoryItemRepository(db).LockForStockChangeAsync(1001, [1003, 1001, 1003]);

        var users = commands.Items.Where(value => value.Sql.Contains("FROM auth.users")).ToArray();
        Assert.Equal(2, users.Length);
        Assert.Equal(users[0].Sql, users[1].Sql);
        Assert.All(users, value => Assert.Equal(1001, Assert.Single(value.Parameters)));
        var advisory = commands.Items.Where(value => value.Sql.Contains("pg_advisory_xact_lock")).ToArray();
        int[][] expected = [[1001, -1001], [1001, 1005], [1001, 0], [1001, 0], [1001, 1002], [1001, 1005], [-8, 55], [-19001, 1], [-19097, 77]];
        Assert.Equal(expected.Length, advisory.Length);
        for (var index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], advisory[index].Parameters.Cast<int>());
        var stock = Assert.Single(commands.Items, value => value.Sql.Contains("FROM inventory.inventory_items"));
        Assert.Equal(1001, stock.Parameters[0]);
        Assert.Equal(new[] { 1001, 1003 }, Assert.IsType<int[]>(stock.Parameters[1]));
        Assert.Contains("ORDER BY id FOR UPDATE", stock.Sql);
        Assert.All(commands.Items, value =>
        {
            Assert.DoesNotContain("1001", value.Sql);
            Assert.DoesNotContain("19001", value.Sql);
            Assert.True(value.HasTransaction);
        });
    }

    [StockPostgresFact]
    public async Task SqlLockRefactorKeepsUserRowLockedAcrossDifferentRepositoryCallersUntilCommit()
    {
        await using var fixture = await Database.CreateAsync();
        await using var first = fixture.Context();
        await using var second = fixture.Context();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();
        await new UserRefreshTokenRepository(first).AcquireUserLockAsync(1001);
        var waiting = new ReservationRepository(second).AcquireCustomerReservationLockAsync(1001, 1001);
        try { await Task.Delay(100); Assert.False(waiting.IsCompleted); }
        finally { await firstTransaction.CommitAsync(); }
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [StockPostgresFact]
    public async Task SqlLockRefactorKeepsTableAndScheduleKeysCompatibleAndReleasesThemOnRollback()
    {
        await using var fixture = await Database.CreateAsync();
        await using var first = fixture.Context();
        await using var other = fixture.Context();
        await using var transaction = await first.Database.BeginTransactionAsync();
        await new TableRepository(first).AcquireReservationLockAsync(1001, 1005);
        await new RestaurantRepository(first).AcquireScheduleLockAsync(1001);
        Assert.False(await CanTakeLockAsync(other, 1001, 1005));
        Assert.False(await CanTakeLockAsync(other, 1001, 0));
        Assert.True(await CanTakeLockAsync(other, 1002, 1005));
        Assert.True(await CanTakeLockAsync(other, 1001, -1005));
        await transaction.RollbackAsync();
        Assert.True(await CanTakeLockAsync(other, 1001, 1005));
        Assert.True(await CanTakeLockAsync(other, 1001, 0));
    }

    [Fact]
    public async Task SqlLockRefactorPreservesNonRelationalScheduleAndStockTransactionGuards()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await new RestaurantRepository(db).AcquireScheduleLockAsync(1001);
        await new RestaurantScheduleRepository(db).AcquireLocksAsync(1001, default);
        Assert.Empty(db.ChangeTracker.Entries());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InventoryItemRepository(db).LockForStockChangeAsync(1001, []));
    }

    /// <summary>İkinci real bağlantının eyni iki hissəli advisory açarı ala bilmədiyini parametrli SQL ilə yoxlayır.</summary>
    private static Task<bool> CanTakeLockAsync(ECafeDbContext db, int namespaceKey, int resourceKey)
        => db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({namespaceKey}, {resourceKey}) AS \"Value\"").SingleAsync();

    private sealed record LockCommand(string Sql, object[] Parameters, bool HasTransaction);

    private sealed class LockCommandCapture : DbCommandInterceptor
    {
        public List<LockCommand> Items { get; } = [];

        /// <summary>Gerçək SQL-i dəyişmədən parametr dəyərlərini və transaction iştirakını sınaq üçün yadda saxlayır.</summary>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Items.Add(new(command.CommandText, command.Parameters.Cast<DbParameter>().Select(value => value.Value!).ToArray(), command.Transaction != null));
            return new(result);
        }
    }
}
