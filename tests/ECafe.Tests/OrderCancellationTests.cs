using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.InventoryMovement;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using BaseFixture = ECafe.Tests.WaiterOrderCreationTests.Fixture;

namespace ECafe.Tests;

public sealed class OrderCancellationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WaiterCancelsOnlyBeforePreparationWithoutTouchingStockPaymentsOrReservation(bool sent, bool accepted)
    {
        await using var f = await Fixture.CreateAsync();
        var order = await f.Db.Orders.SingleAsync();
        if (sent) order.SentToKitchenAt = DateTime.UtcNow.AddMinutes(-2);
        if (accepted) { order.StatusId = 2002; order.AcceptedAt = DateTime.UtcNow.AddMinutes(-1); }
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Base.Manager.GetByIdAsync(1, 1, order.Id)).CanCancel);
        var result = await f.Cancel();
        Assert.Equal(2007, result.StatusId);
        Assert.False(result.CanCancel);
        Assert.NotNull(result.CancelledAt);
        Assert.Equal(TimeSpan.Zero, result.CancelledAt!.Value.Offset);
        Assert.Equal("Səhv sifariş", result.CancellationReason);
        Assert.Equal(7m, result.Subtotal);
        Assert.Equal(2, Assert.Single(result.Items).Quantity);
        Assert.Empty(await f.Db.InventoryMovements.ToListAsync());
        Assert.Empty(await f.Db.Payments.ToListAsync());
        Assert.Equal(7001, (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).StatusId);
        Assert.Null((await f.Db.Reservations.SingleAsync()).CancelledAt);
        Assert.False((await f.Base.Manager.GetByIdAsync(1, 1, order.Id)).CanCancel);
        var bill = TableSessionBillingPolicy.Calculate(await f.Db.TableSessions.Include(s => s.Orders).ThenInclude(o => o.OrderItems)
            .SingleAsync(s => s.Id == 1));
        Assert.Equal(0, bill.TotalAmount);
        Assert.False(bill.HasUnservedOrders);
    }

    [Fact]
    public async Task CustomerCanCancelWaiterCreatedOrderInOwnSessionAndCannotSeeStaffIds()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(3, RoleCode.Customer);
        var result = await f.Service.CancelForCustomerAsync(1, f.OrderId, new("  Səhv sifariş  "));
        Assert.Equal(2007, result.StatusId);
        Assert.Equal("Səhv sifariş", result.CancellationReason);
        Assert.False(result.CanCancel);
        Assert.Equal(3, (await f.Db.Orders.SingleAsync()).CancelledByUserId);
        Assert.DoesNotContain("CancelledByUserId", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Contains(f.Sent, n => n.UserId == 1);
    }

    [Fact]
    public async Task ReplayKeepsFirstReasonTimeActorAuditAndNotifications()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.Cancel();
        f.Actor(3, RoleCode.Customer);
        var replay = await f.Service.CancelForCustomerAsync(1, f.OrderId, new("Başqa səbəb"));
        Assert.Equal(first.CancelledAt, replay.CancelledAt);
        Assert.Equal(first.CancellationReason, replay.CancellationReason);
        Assert.Equal(1, (await f.Db.Orders.SingleAsync()).CancelledByUserId);
        f.Base.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderCancelled, It.IsAny<object>(),
            AuditEntityTypes.Order, f.OrderId, null), Times.Once);
        Assert.Single(f.Sent);
    }

    [Theory]
    [InlineData(2003)] [InlineData(2004)] [InlineData(2005)] [InlineData(2006)] [InlineData(2007)] [InlineData(2008)]
    public async Task PreparingAndTerminalOrScheduledOrdersCannotCancel(int status)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Orders.SingleAsync()).StatusId = status;
        await f.Db.SaveChangesAsync();
        Assert.False((await f.Base.Manager.GetByIdAsync(1, 1, f.OrderId)).CanCancel);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Cancel());
        Assert.Equal(ErrorCode.OrderCannotCancel, error.Code);
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData("preparedTime")] [InlineData("readyTime")] [InlineData("servedTime")]
    [InlineData("orderClosed")] [InlineData("sessionClosed")] [InlineData("pending")] [InlineData("review")]
    [InlineData("orderPaid")]
    public async Task MilestonesClosedSessionsAndPaymentsBlockStaleCancelButton(string state)
    {
        await using var f = await Fixture.CreateAsync();
        var order = await f.Db.Orders.SingleAsync();
        var session = await f.Db.TableSessions.SingleAsync(s => s.Id == 1);
        if (state == "preparedTime") order.PreparingAt = DateTime.UtcNow;
        if (state == "readyTime") order.ReadyAt = DateTime.UtcNow;
        if (state == "servedTime") order.ServedAt = DateTime.UtcNow;
        if (state == "orderClosed") order.ClosedAt = DateTime.UtcNow;
        if (state == "sessionClosed") session.ClosedAt = DateTime.UtcNow;
        if (state is "pending" or "review" or "orderPaid") f.Db.Payments.Add(new Payment
        {
            Id = 99, RestaurantId = 1, TableSessionId = 1, OrderId = state == "orderPaid" ? order.Id : null,
            Amount = 7, PaymentStatusId = state == "pending" ? 3001 : 3002, RequiresRefund = state == "review"
        });
        await f.Db.SaveChangesAsync();
        var eligible = await f.Db.Orders.Where(OrderCancellationPolicy.Cancellable).AnyAsync();
        Assert.False(eligible);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Cancel());
        Assert.Null(order.CancelledAt);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConsumptionHistoryEvenSoftDeletedPreventsCancellationAndNeverRestoresStock(bool deleted)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.InventoryMovements.Add(new InventoryMovement { Id = 90, RestaurantId = 1,
            RelatedOrderId = f.OrderId, MovementTypeId = (int)InventoryMovementTypeCode.OrderConsumption,
            QuantityChange = -1, IsDeleted = deleted });
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Cancel());
        Assert.Equal(ErrorCode.OrderCancellationStockConflict, error.Code);
        Assert.Equal(-1, (await f.Db.InventoryMovements.IgnoreQueryFilters().SingleAsync()).QuantityChange);
        Assert.Null((await f.Db.Orders.SingleAsync()).CancelledAt);
    }

    [Theory]
    [InlineData(RoleCode.Manager)] [InlineData(RoleCode.Owner)] [InlineData(RoleCode.Kitchen)] [InlineData(RoleCode.SuperAdmin)]
    public async Task GeneralOrderPermissionCannotBypassLiveWaiterRole(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.UserRestaurants.SingleAsync()).RoleId = (int)role;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Cancel());
    }

    [Theory]
    [InlineData("customer")] [InlineData("waiter")] [InlineData("restaurant")]
    [InlineData("foreignOrder")] [InlineData("deleted")]
    public async Task ForeignAndDeletedObjectsCannotBeCancelled(string state)
    {
        await using var f = await Fixture.CreateAsync();
        if (state == "customer")
        {
            f.Actor(2, RoleCode.Customer);
            await Assert.ThrowsAsync<NotFoundException>(() => f.Service.CancelForCustomerAsync(1, f.OrderId, new("Səhv sifariş")));
            return;
        }
        var order = await f.Db.Orders.SingleAsync();
        if (state == "waiter") (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).WaiterUserId = 2;
        if (state == "restaurant") order.RestaurantId = 2;
        if (state == "foreignOrder") order.TableSessionId = 2;
        if (state == "deleted") order.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Cancel());
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData("role")] [InlineData("preparing")] [InlineData("sessionClosed")] [InlineData("customer")]
    public async Task ChangesWhileWaitingForLockAreRechecked(string state)
    {
        await using var f = await Fixture.CreateAsync();
        if (state == "customer") f.Actor(3, RoleCode.Customer);
        f.BeforeLock = async () =>
        {
            if (state == "role") (await f.Db.UserRestaurants.SingleAsync()).RoleId = (int)RoleCode.Manager;
            if (state == "preparing") (await f.Db.Orders.SingleAsync()).PreparingAt = DateTime.UtcNow;
            if (state == "sessionClosed") (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).ClosedAt = DateTime.UtcNow;
            if (state == "customer") (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).CustomerUserId = 2;
            await f.Db.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => state == "customer"
            ? f.Service.CancelForCustomerAsync(1, f.OrderId, new("Səhv sifariş")) : (Task)f.Cancel());
        Assert.Null((await f.Db.Orders.SingleAsync()).CancelledAt);
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData("")] [InlineData("ab")]
    public async Task EmptyReasonDoesNotMutateAnything(string reason)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CancelForWaiterAsync(1, 1, f.OrderId, new(reason)));
        Assert.Null((await f.Db.Orders.SingleAsync()).CancelledAt);
    }

    [Fact]
    public async Task OverlongReasonIsRejectedBeforeAnyWrite()
    {
        await using var f = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CancelForWaiterAsync(1, 1, f.OrderId, new(new string('a', 501))));
        Assert.Equal(ErrorCode.OrderCancellationReasonInvalid, error.Code);
        Assert.Null((await f.Db.Orders.SingleAsync()).CancelledAt);
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InactiveUserCannotCancelThroughEitherEndpoint(bool customer)
    {
        await using var f = await Fixture.CreateAsync();
        var id = customer ? 3 : 1;
        if (customer) f.Actor(id, RoleCode.Customer);
        (await f.Db.Users.SingleAsync(user => user.Id == id)).IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => customer
            ? f.Service.CancelForCustomerAsync(1, f.OrderId, new("Səhv sifariş")) : (Task)f.Cancel());
        Assert.Null((await f.Db.Orders.SingleAsync()).CancelledAt);
    }

    [Fact]
    public async Task CurrentWaiterMayCancelOrderCreatedByPreviousWaiter()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Orders.SingleAsync()).WaiterUserId = 2;
        await f.Db.SaveChangesAsync();
        Assert.Equal(2007, (await f.Cancel()).StatusId);
        Assert.Equal(1, (await f.Db.Orders.SingleAsync()).CancelledByUserId);
    }

    [Theory]
    [InlineData("az")] [InlineData("en")] [InlineData("ru")]
    public void CancellationTextsAreCompleteAndNotificationTemplatePreservesArguments(string language)
    {
        var bootstrap = new TranslationBootstrap();
        var messages = bootstrap.Snapshot.ForLanguage(language).Messages;
        foreach (var key in new[] { "api.error.OrderCannotCancel", "api.error.OrderCancellationReasonInvalid",
                     "api.error.OrderCancellationStockConflict", "notification.orderCancelled.title", "ui.orderCancel.action" })
            Assert.False(string.IsNullOrWhiteSpace(messages[key]));
        var match = bootstrap.NotificationSourceMatcher.Match("Masa-2 üçün #81 sifarişi ləğv edildi.");
        Assert.Equal("notification.orderCancelled.message", match!.Key);
        Assert.Equal("81", match.Arguments["orderId"]);
        Assert.Equal("Masa-2", match.Arguments["tableName"]);
    }

    [Fact]
    public async Task TwoParallelCancellationsProduceOneDurableDecision()
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1, 1);
        await using var first = await Fixture.CreateAsync(store, gate);
        first.Db.ChangeTracker.Clear();
        await using var second = await Fixture.CreateAsync(store, gate);
        var results = await Task.WhenAll(first.Cancel(), second.Cancel());
        Assert.Equal(results[0].CancelledAt, results[1].CancelledAt);
        Assert.Equal(1, first.Sent.Count + second.Sent.Count);
        Assert.Equal(1, first.Base.Audit.Invocations.Count(x => Equals(x.Arguments[1], AuditActions.OrderCancelled)) +
            second.Base.Audit.Invocations.Count(x => Equals(x.Arguments[1], AuditActions.OrderCancelled)));
    }

    [Fact]
    public async Task NotificationFailureDoesNotCommitCancellation()
    {
        await using var f = await Fixture.CreateAsync();
        f.Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Cancel());
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CustomerCanCancelProjectionTranslatesToPostgresWithoutClientEvaluation()
    {
        using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options);
        var sql = CustomerOrderTrackingMapper.ProjectOrders(db.Orders.Where(o => o.TableSessionId == 1)).ToQueryString();
        Assert.Contains("EXISTS", sql);
        Assert.Contains("cancelled_at", sql);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public BaseFixture Base { get; }
        public ECafeDbContext Db => Base.Context;
        public int OrderId { get; }
        public OrderCancellationManager Service { get; }
        public Func<Task>? BeforeLock { get; set; }
        public List<CreateNotificationRequest> Sent { get; } = [];
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();

        /// <summary>Mövcud ofisiant seed-lərini saxlayır; ayrıca transaction/kilid sərhədini ləğv üçün idarə edir.</summary>
        private Fixture(BaseFixture source, int orderId, SemaphoreSlim? gate)
        {
            Base = source;
            OrderId = orderId;
            var locked = false;
            var table = new Mock<ITableRepository>();
            table.Setup(t => t.AcquireReservationLockAsync(1, 1, It.IsAny<CancellationToken>())).Returns(async () =>
            {
                if (BeforeLock != null) await BeforeLock();
                if (gate != null) { await gate.WaitAsync(); locked = true; }
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
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(Transaction.Object);
            Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>())).Callback<CreateNotificationRequest>(Sent.Add).Returns(Task.CompletedTask);
            Service = new OrderCancellationManager(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == source.Http), Mock.Of<IMapper>(),
                new ConfigurationBuilder().Build(), new OrderRepository(Db), new TableSessionRepository(Db), table.Object,
                new UserRepository(Db), new UserRestaurantRepository(Db), new InventoryMovementRepository(Db),
                transactions.Object, source.Audit.Object, Notifications.Object, TimeProvider.System);
        }

        /// <summary>Təmiz, öz müştərisi və cari ofisiantı olan bir sessiya sifarişi yaradır; paralel testlərdə eyni bazanı paylaşır.</summary>
        public static async Task<Fixture> CreateAsync(string? store = null, SemaphoreSlim? gate = null)
        {
            var source = await BaseFixture.CreateAsync(store);
            if (!await source.Context.Orders.AnyAsync()) await source.CreateAsync(new CreateWaiterOrderRequest
                { Items = [new CreateOrderLineRequest { ItemId = 1, Quantity = 2 }] });
            source.Audit.Invocations.Clear();
            var id = await source.Context.Orders.Select(o => o.Id).SingleAsync();
            return new Fixture(source, id, gate);
        }

        /// <summary>İstifadəçi kontekstini dəyişir; canlı DB rolu ayrıca yoxlanılır.</summary>
        public void Actor(int id, RoleCode role) => Base.Http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", id.ToString()), new Claim("restaurantId", "1"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));

        /// <summary>Varsayılan ləğv sorğusunu cari ofisiant kontekstində çağırır.</summary>
        public Task<SessionOrderResponse> Cancel() => Service.CancelForWaiterAsync(1, 1, OrderId, new("Səhv sifariş"));

        /// <summary>Test bazasının kontekstini bağlayır.</summary>
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }
}
