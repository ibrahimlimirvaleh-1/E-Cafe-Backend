using System.Data.Common;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Dashboard;
using ECafe.Infrastructure.Repositories.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [Fact]
    public async Task AppSqlRefactorKeepsInMemoryOutboxFiltersAndTracking()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        await SeedBatchEventsAsync(db, now);
        db.ChangeTracker.Clear();
        var reader = new OutboxBatchReader(db);
        Assert.Equal(new[] { BatchId(1), BatchId(3) }, (await reader.ReadRealtimeAsync(2, now, default)).Select(value => value.Id));
        Assert.Equal(BatchId(4), Assert.Single(await reader.ReadMobilePushAsync(10, default)).Id);
        var tracked = await reader.ReadRealtimeAsync(10, now, default);
        Assert.Equal(new[] { BatchId(1), BatchId(3), BatchId(8) }, tracked.Select(value => value.Id));
        Assert.All(tracked, value => Assert.Equal(EntityState.Unchanged, db.Entry(value).State));
        tracked[0].ProcessedAt = now;
        await db.SaveChangesAsync();
        Assert.Equal(new[] { BatchId(3), BatchId(8) }, (await reader.ReadRealtimeAsync(10, now, default)).Select(value => value.Id));
    }

    [StockPostgresFact]
    public async Task AppSqlRefactorKeepsPostgresBatchParametersAndSeparateRetryRules()
    {
        await using var fixture = await Database.CreateAsync();
        var capture = new OutboxReadCapture();
        await using var db = fixture.Context(capture);
        var now = DateTime.UtcNow;
        await SeedBatchEventsAsync(db, now);
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reader = new OutboxBatchReader(db);
        Assert.Equal(new[] { BatchId(1), BatchId(3) }, (await reader.ReadRealtimeAsync(2, now, default)).Select(value => value.Id));
        Assert.Equal(BatchId(4), Assert.Single(await reader.ReadMobilePushAsync(10, default)).Id);
        Assert.Equal(2, capture.Commands.Count);
        Assert.Equal(new object[] { OutboxEventTypes.OrderRealtimeRequested, OutboxEventTypes.NotificationRealtimeRequested, now, 2 }, capture.Commands[0].Parameters);
        Assert.Equal(new object[] { OutboxEventTypes.MobilePushRequested, 10 }, capture.Commands[1].Parameters);
        Assert.All(capture.Commands, value =>
        {
            Assert.True(value.HasTransaction);
            Assert.Contains("FOR UPDATE SKIP LOCKED", value.Sql);
            Assert.Contains("ORDER BY \"OccurredAt\", \"Id\"", value.Sql);
            Assert.DoesNotContain(OutboxEventTypes.MobilePushRequested, value.Sql);
            Assert.DoesNotContain(OutboxEventTypes.OrderRealtimeRequested, value.Sql);
        });
    }

    [StockPostgresFact]
    public async Task AppSqlRefactorKeepsConcurrentBatchWorkersIndependentAndRollbackReleasesRows()
    {
        await using var fixture = await Database.CreateAsync();
        var now = DateTime.UtcNow;
        await using (var setup = fixture.Context()) await SeedBatchEventsAsync(setup, now);
        await using var first = fixture.Context();
        await using var second = fixture.Context();
        await using var third = fixture.Context();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();
        await using var thirdTransaction = await third.Database.BeginTransactionAsync();
        Assert.Equal(BatchId(1), Assert.Single(await new OutboxBatchReader(first).ReadRealtimeAsync(1, now, default)).Id);
        Assert.Equal(new[] { BatchId(3), BatchId(8) }, (await new OutboxBatchReader(second).ReadRealtimeAsync(10, now, default)
            .WaitAsync(TimeSpan.FromSeconds(5))).Select(value => value.Id));
        Assert.Equal(BatchId(4), Assert.Single(await new OutboxBatchReader(second).ReadMobilePushAsync(10, default)).Id);
        Assert.Empty(await new OutboxBatchReader(third).ReadMobilePushAsync(10, default).WaitAsync(TimeSpan.FromSeconds(5)));
        await firstTransaction.RollbackAsync();
        Assert.Equal(BatchId(1), Assert.Single(await new OutboxBatchReader(third).ReadRealtimeAsync(10, now, default)).Id);
        await secondTransaction.CommitAsync();
        Assert.Equal(BatchId(4), Assert.Single(await new OutboxBatchReader(third).ReadMobilePushAsync(10, default)).Id);
    }

    [StockPostgresFact]
    public async Task AppSqlRefactorKeepsReportSnapshotReadOnlyAndRepeatableRead()
    {
        await using var fixture = await Database.CreateAsync();
        var probe = new ReportTransactionProbe();
        await using var db = fixture.Context(probe);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", DateTimeOffset.UtcNow);
        var report = await new DashboardRepository(db).GetReportAsync(new(1001, RoleCode.SuperAdmin, null, false),
            period, DashboardReportKind.Orders, null, "all", 1, false, default);
        Assert.NotNull(report);
        Assert.Equal("on", probe.ReadOnly);
        Assert.Equal("repeatable read", probe.Isolation);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(1, await db.Users.CountAsync());
    }

    /// <summary>Filtrləmə və səhifə həddi üçün sabit, şəxsi məlumat daşımayan hadisə identifikatoru qaytarır.</summary>
    private static Guid BatchId(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:D12}");

    /// <summary>Realtime retry, işlənmiş sətir, ayrı event tipi və mobil push-un fərqli kilid filtrini eyni test məlumatında saxlayır.</summary>
    private static async Task SeedBatchEventsAsync(ECafeDbContext db, DateTime now)
    {
        var rows = Enumerable.Range(1, 8).Select(id => new OutboxEvent { Id = BatchId(id),
            EventType = OutboxEventTypes.NotificationRealtimeRequested, AggregateType = "Synthetic", AggregateId = 1,
            Payload = "{}", OccurredAt = now.AddMinutes(id - 10) }).ToArray();
        rows[0].EventType = OutboxEventTypes.OrderRealtimeRequested;
        rows[1].LockedUntil = now.AddHours(1);
        rows[2].LockedUntil = now.AddMinutes(-1);
        rows[3].EventType = OutboxEventTypes.MobilePushRequested;
        rows[3].LockedUntil = now.AddHours(1);
        rows[4].ProcessedAt = now;
        rows[5].EventType = "Synthetic.Unrelated";
        rows[6].EventType = OutboxEventTypes.MobilePushRequested;
        rows[6].ProcessedAt = now;
        db.OutboxEvents.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private sealed record OutboxReadCommand(string Sql, object[] Parameters, bool HasTransaction);

    private sealed class OutboxReadCapture : DbCommandInterceptor
    {
        public List<OutboxReadCommand> Commands { get; } = [];

        /// <summary>Batch SQL-inə müdaxilə etmədən parametrli icranı və eyni transaction istifadəsini ölçür.</summary>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT * FROM audit.outbox_events", StringComparison.Ordinal))
                Commands.Add(new(command.CommandText, command.Parameters.Cast<DbParameter>().Select(value => value.Value!).ToArray(), command.Transaction != null));
            return new(result);
        }
    }

    private sealed class ReportTransactionProbe : DbCommandInterceptor
    {
        public string? ReadOnly { get; private set; }
        public string? Isolation { get; private set; }

        /// <summary>Hesabatın ilk real oxunuşunda eyni DB bağlantısından transaction rejimini müşahidə edir; məlumat dəyişmir.</summary>
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (ReadOnly == null && command.Transaction != null)
            {
                await using var probe = command.Connection!.CreateCommand();
                probe.Transaction = command.Transaction;
                probe.CommandText = "SHOW transaction_read_only";
                ReadOnly = (string?)await probe.ExecuteScalarAsync(cancellationToken);
                probe.CommandText = "SHOW transaction_isolation";
                Isolation = (string?)await probe.ExecuteScalarAsync(cancellationToken);
            }
            return result;
        }
    }
}
