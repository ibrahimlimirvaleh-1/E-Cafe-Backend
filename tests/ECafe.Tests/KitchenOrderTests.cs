using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class KitchenOrderTests
{
    [Fact]
    public async Task SendPersistsSnapshotWithoutAcceptingOrChargingAndNotifiesOnlyActiveKitchenOnce()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.Service.SendAsync(1, 1, 1);
        var replay = await f.Service.SendAsync(1, 1, 1);
        Assert.Equal(first.SentToKitchenAt, replay.SentToKitchenAt);
        Assert.Equal(2001, first.StatusId);
        Assert.Equal(7m, first.Subtotal);
        Assert.Equal("Tea", Assert.Single(first.Items).Name);
        Assert.NotNull(first.SentToKitchenAt);
        var order = await f.Db.Orders.SingleAsync(o => o.Id == 1);
        Assert.Null(order.AcceptedAt);
        Assert.Null(order.PreparingAt);
        Assert.Empty(await f.Db.Payments.ToListAsync());
        Assert.Empty(await f.Db.InventoryMovements.ToListAsync());
        Assert.Equal(7001, (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).StatusId);
        var notification = Assert.Single(f.Sent);
        Assert.Equal(2, notification.UserId);
        Assert.Equal(1, notification.RestaurantId);
        Assert.Equal((int)NotificationType.OrderCreated, notification.TypeId);
        Assert.Equal("Order", notification.RelatedEntityType);
        Assert.Contains("Masa-1", notification.Message);
        Assert.DoesNotContain("7.00", notification.Message);
        f.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderSentToKitchen,
            It.IsAny<object>(), AuditEntityTypes.Order, 1, null), Times.Once);
    }

    [Theory]
    [InlineData(2)] // Another restaurant.
    [InlineData(3)] // Other waiter's order.
    [InlineData(4)] // Closed session.
    [InlineData(8)] // Deleted order.
    [InlineData(999)]
    public async Task MissingForeignAndDeletedOrdersCannotBeSent(int orderId)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SendAsync(1, 1, orderId));
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData(OrderStatus.Accepted)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Served)]
    [InlineData(OrderStatus.Closed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Scheduled)]
    public async Task UnsentNonCreatedOrderCannotEnterTheQueue(OrderStatus status)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Orders.SingleAsync(o => o.Id == 1)).StatusId = 2000 + (int)status;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SendAsync(1, 1, 1));
        Assert.Equal(ErrorCode.OrderCannotSendToKitchen, error.Code);
        Assert.Empty(f.Sent);
    }

    [Fact]
    public async Task SendReplayDoesNotResetAnAdvancedKitchenStatus()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.SendAsync(1, 1, 1);
        (await f.Db.Orders.SingleAsync(o => o.Id == 1)).StatusId = 2004;
        await f.Db.SaveChangesAsync();
        Assert.Equal(2004, (await f.Service.SendAsync(1, 1, 1)).StatusId);
        Assert.Single(f.Sent);
    }

    [Fact]
    public async Task EmptyOrderIsNotSentAndAnOrderWithoutKitchenStaffIsStillDurable()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.OrderItems.RemoveRange(await f.Db.OrderItems.Where(line => line.OrderId == 1).ToListAsync());
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SendAsync(1, 1, 1));
        f.Db.OrderItems.Add(new OrderItem { Id = 100, OrderId = 1, ItemId = 1, NameSnapshot = "Tea", Quantity = 1 });
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).IsActive = false;
        await f.Db.SaveChangesAsync();
        Assert.NotNull((await f.Service.SendAsync(1, 1, 1)).SentToKitchenAt);
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("reassigned")]
    [InlineData("roleChanged")]
    [InlineData("tableChanged")]
    public async Task DispatchRechecksSessionAndRoleAfterTableLock(string change)
    {
        await using var f = await Fixture.CreateAsync();
        f.BeforeLock = async () =>
        {
            var session = await f.Db.TableSessions.SingleAsync(s => s.Id == 1);
            if (change == "closed") session.ClosedAt = DateTime.UtcNow;
            if (change == "reassigned") session.WaiterUserId = 3;
            if (change == "tableChanged") session.TableId = 2;
            if (change == "roleChanged") (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 1)).RoleId = 6;
            await f.Db.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => f.Service.SendAsync(1, 1, 1));
        Assert.Null((await f.Db.Orders.SingleAsync(o => o.Id == 1)).SentToKitchenAt);
        Assert.Empty(f.Sent);
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParallelDispatchOfTheSameOrderNotifiesOnlyOnce()
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1, 1);
        await using var a = await Fixture.CreateAsync(store, gate);
        await using var b = await Fixture.CreateAsync(store, gate);
        var results = await Task.WhenAll(a.Service.SendAsync(1, 1, 1), b.Service.SendAsync(1, 1, 1));
        Assert.Equal(results[0].SentToKitchenAt, results[1].SentToKitchenAt);
        Assert.Equal(1, a.Sent.Count + b.Sent.Count);
    }

    [Fact]
    public async Task QueueIsTenantScopedSentOnlyActiveFifoAndPaginatedWithoutFinancialFields()
    {
        await using var f = await Fixture.CreateAsync();
        f.LoginKitchen();
        var page = await f.Service.GetQueueAsync(1, new PaginationFilter(1, 2));
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(new[] { 9, 10 }, page.Items.Select(o => o.Id));
        Assert.Equal(new[] { 11, 13 }, (await f.Service.GetQueueAsync(1, new PaginationFilter(2, 2))).Items.Select(o => o.Id));
        Assert.Empty((await f.Service.GetQueueAsync(1, new PaginationFilter(int.MaxValue, 100))).Items);
        var json = JsonSerializer.Serialize(page);
        Assert.DoesNotContain("UnitPrice", json);
        Assert.DoesNotContain("Subtotal", json);
        Assert.DoesNotContain("Customer", json);
        Assert.DoesNotContain("Payment", json);
        Assert.Equal("Tea", Assert.Single(page.Items[0].Items).Name);
        Assert.Equal("Masa-1", page.Items[0].TableName);
        Assert.Equal("Asia/Baku", page.Items[0].TimeZone);
    }

    [Fact]
    public async Task QueueRetainsYesterdayAndSentOrdersEvenIfTableSessionWasClosed()
    {
        await using var f = await Fixture.CreateAsync();
        var order = await f.Db.Orders.SingleAsync(o => o.Id == 13);
        order.SentToKitchenAt = DateTime.UtcNow.AddDays(-2);
        (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).ClosedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        f.LoginKitchen();
        var queue = await f.Service.GetQueueAsync(1, new PaginationFilter());
        Assert.Equal(13, queue.Items[0].Id);
    }

    [Theory]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task StaleKitchenTokenCannotOverrideLiveRole(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        f.LoginKitchen();
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).RoleId = (int)role;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetQueueAsync(1, new PaginationFilter()));
    }

    [Fact]
    public async Task InactiveStaffRestaurantAndCrossRestaurantQueueAreDenied()
    {
        await using var f = await Fixture.CreateAsync();
        f.LoginKitchen();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetQueueAsync(2, new PaginationFilter()));
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetQueueAsync(1, new PaginationFilter()));
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).IsActive = true;
        (await f.Db.Restaurants.SingleAsync(r => r.Id == 1)).IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetQueueAsync(1, new PaginationFilter()));
    }

    [Fact]
    public async Task FailedNotificationOrAuditDoesNotCommitTheDispatchTransaction()
    {
        await using var f = await Fixture.CreateAsync();
        f.Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SendAsync(1, 1, 1));
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        await using var a = await Fixture.CreateAsync();
        a.Audit.Setup(x => x.RecordRestaurantActionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(),
            It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => a.Service.SendAsync(1, 1, 1));
        a.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelledRequestDoesNotSend()
    {
        await using var f = await Fixture.CreateAsync();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.SendAsync(1, 1, 1, source.Token));
        Assert.Empty(f.Sent);
    }

    [Fact]
    public async Task QueueTranslatesForPostgresAndHasAnIndexForRestaurantAndFifo()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").AddInterceptors(new TranslationProbe()).Options);
        var roles = new Mock<ECafe.Application.Repositories.UserRestaurant.IUserRestaurantRepository>();
        roles.Setup(r => r.GetActiveRoleIdAsync(2, 1)).ReturnsAsync((int)RoleCode.Kitchen);
        var service = new KitchenOrderManager(Accessor(2, 6), Mock.Of<IMapper>(), new ConfigurationBuilder().Build(),
            new OrderRepository(db), Mock.Of<ECafe.Application.Services.TableSessions.Abstract.ITableSessionService>(),
            Mock.Of<ITableRepository>(), roles.Object, Mock.Of<IApplicationDbTransactionFactory>(),
            Mock.Of<INotificationService>(), Mock.Of<IAuditLogService>(), TimeProvider.System);
        await Assert.ThrowsAsync<TranslationVerifiedException>(() => service.GetQueueAsync(1, new PaginationFilter()));
        var index = db.Model.FindEntityType(typeof(Order))!.GetIndexes().Single(i => i.GetDatabaseName() == "orders_kitchen_queue_idx");
        Assert.Equal(new[] { "RestaurantId", "SentToKitchenAt", "Id" }, index.Properties.Select(p => p.Name));
        Assert.Contains("sent_to_kitchen_at IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void ControllersRequireCorrectPermissionsAndErrorsHaveDistinctCodes()
    {
        Assert.NotNull(typeof(OrderFlowController).GetMethod(nameof(OrderFlowController.SendToKitchen))!
            .GetCustomAttribute<RequireActiveRestaurantContractAttribute>());
        Assert.NotNull(typeof(KitchenController).GetCustomAttribute<HasPermissionAttribute>());
        Assert.NotNull(typeof(OrderFlowController).GetCustomAttribute<HasPermissionAttribute>());
        Assert.NotEqual((int)ErrorCode.OrderNotFound, (int)ErrorCode.TableSessionReviewRequired);
        Assert.Contains("sifariş tapılmadı", new ErrorMessageProvider().GetMessage(ErrorCode.OrderNotFound));
    }

    /// <summary>Test istifadəçisini restoran konteksti ilə qurur; aktiv rol ayrıca həqiqi repository ilə yoxlanılır.</summary>
    private static IHttpContextAccessor Accessor(int userId, int roleId)
        => new TestAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", userId.ToString()), new Claim("restaurantId", "1"), new Claim(ClaimTypes.Role, roleId.ToString())], "test")) } };

    private sealed class TranslationVerifiedException : Exception;
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
    private sealed class TranslationProbe : DbConnectionInterceptor
    {
        /// <summary>EF SQL tərcüməsindən sonra real bağlantı açmadan dayanır.</summary>
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new TranslationVerifiedException();
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; }
        public KitchenOrderManager Service { get; }
        public Mock<IAuditLogService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public List<CreateNotificationRequest> Sent { get; } = [];
        public Func<Task>? BeforeLock { get; set; }
        private readonly IHttpContextAccessor _accessor = Accessor(1, 4);

        /// <summary>EF repository-lərini istifadə edir; transaction/kilidi ayrıca test sərhədi kimi idarə edir.</summary>
        private Fixture(ECafeDbContext db, SemaphoreSlim? gate)
        {
            Db = db;
            var sessions = new TableSessionRepository(db);
            var assignments = new UserRestaurantRepository(db);
            var config = new ConfigurationBuilder().Build();
            var access = new TableSessionManager(_accessor, Mock.Of<IMapper>(), config, sessions, assignments);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(Transaction.Object);
            var tables = new Mock<ITableRepository>();
            var locked = false;
            tables.Setup(t => t.AcquireReservationLockAsync(1, 1, It.IsAny<CancellationToken>()))
                .Returns(async (int restaurantId, int tableId, CancellationToken token) =>
                {
                    if (BeforeLock != null) await BeforeLock();
                    if (gate != null) { await gate.WaitAsync(token); locked = true; }
                });
            Transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return Task.CompletedTask;
            });
            Transaction.Setup(t => t.DisposeAsync()).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return ValueTask.CompletedTask;
            });
            Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>()))
                .Callback<CreateNotificationRequest>(Sent.Add).Returns(Task.CompletedTask);
            Service = new KitchenOrderManager(_accessor, Mock.Of<IMapper>(), config, new OrderRepository(db), access,
                tables.Object, assignments, transactions.Object, Notifications.Object, Audit.Object, TimeProvider.System);
        }

        /// <summary>Mətbəx ekranını eyni bazada başqa istifadəçi kimi yoxlamaq üçün giriş kontekstini dəyişir.</summary>
        public void LoginKitchen(int userId = 2) => _accessor.HttpContext = Accessor(userId, 6).HttpContext;

        /// <summary>İki restoran, aktiv/deaktiv işçilər və göndərilmiş/göndərilməmiş sifarişlər yaradır.</summary>
        public static async Task<Fixture> CreateAsync(string? store = null, SemaphoreSlim? gate = null)
        {
            var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
                .UseInMemoryDatabase(store ?? Guid.NewGuid().ToString()).Options);
            if (!await db.Restaurants.AnyAsync())
            {
                db.Roles.AddRange(new Role { Id = 4, Name = "Waiter" }, new Role { Id = 6, Name = "Kitchen" });
                for (var i = 1; i <= 2; i++)
                {
                    db.Restaurants.Add(new Restaurant { Id = i, Name = $"Restaurant{i}", Phone = i.ToString(), Location = "Baku", TimeZone = "Asia/Baku", IsActive = true });
                    db.Tables.Add(new Table { Id = i, RestaurantId = i, TableNo = 1, Capacity = 4, IsActive = true });
                }
                for (var i = 1; i <= 6; i++)
                {
                    db.Users.Add(new User { Id = i, Name = "Staff", Surname = i.ToString(), Email = $"s{i}@example.test", Phone = i.ToString(), Password = "test", IsActive = i != 6 });
                    db.UserRestaurants.Add(new UserRestaurant { Id = i, UserId = i, RestaurantId = i == 4 ? 2 : 1,
                        RoleId = i is 1 or 3 ? 4 : 6, IsActive = i != 5 });
                }
                for (var i = 1; i <= 3; i++)
                    db.TableSessions.Add(new TableSession { Id = i, RestaurantId = i == 3 ? 2 : 1, TableId = i == 3 ? 2 : 1,
                        WaiterUserId = i == 2 ? 3 : 1, OpenedAt = DateTime.UtcNow, StatusId = 7001 });
                var at = DateTime.UtcNow.AddHours(-2);
                for (var i = 1; i <= 14; i++)
                {
                    var status = i switch { 5 => 5, 6 => 7, 7 => 8, 9 => 2, 10 => 3, 11 => 4, 12 => 6, _ => 1 };
                    db.Orders.Add(new Order { Id = i, RestaurantId = i == 2 ? 2 : 1, TableId = i == 2 ? 2 : 1,
                        TableSessionId = i == 2 ? 3 : i is 3 or 4 ? 2 : 1, WaiterUserId = i is 3 or 4 ? 3 : 1,
                        SourceId = (int)OrderSource.Waiter, OpenedAt = at.AddMinutes(-20), StatusId = 2000 + status,
                        IsDeleted = i == 8, ClosedAt = i == 14 ? at : null, SentToKitchenAt = i >= 5 ? at : null,
                        OrderItems = [new OrderItem { ItemId = 1, NameSnapshot = "Tea", Quantity = 2, UnitPrice = 3.5m,
                            UnitPriceSnapshot = 3.5m, LineTotal = 7, Note = "No sugar" }] });
                }
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
            return new Fixture(db, gate);
        }

        /// <summary>Test bazasının resurslarını sərbəst buraxır.</summary>
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
