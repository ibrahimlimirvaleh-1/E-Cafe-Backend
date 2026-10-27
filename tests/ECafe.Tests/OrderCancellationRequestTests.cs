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
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
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

public sealed class OrderCancellationRequestTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task CustomerViaWaiterAndWaiterDirectlyReachKitchenWithoutChangingOrderOrStock(bool customer)
    {
        await using var f = await Fixture.CreateAsync();
        Assert.True((await f.Base.Manager.GetByIdAsync(1, 1, f.Id)).CanRequestCancellation);
        if (customer)
        {
            f.Actor(3, RoleCode.Customer);
            var first = await f.Service.RequestForCustomerAsync(1, f.Id, new("Plan dəyişdi"));
            Assert.Equal(1, first.CancellationRequest!.StatusId);
            Assert.Equal(2003, first.StatusId);
            Assert.False(first.CanRequestCancellation);
            Assert.Contains(f.Sent, notification => notification.UserId == 1);
            Assert.DoesNotContain("ByUserId", System.Text.Json.JsonSerializer.Serialize(first));
            f.Actor(1, RoleCode.Waiter);
            var view = await f.Base.Manager.GetByIdAsync(1, 1, f.Id);
            Assert.True(view.CanForwardCancellationRequest);
            var forwarded = await f.Service.ForwardAsync(1, 1, f.Id);
            Assert.Equal(2, forwarded.CancellationRequest!.StatusId);
            Assert.NotNull(forwarded.CancellationRequest.ForwardedAt);
        }
        else Assert.Equal(2, (await f.Request()).CancellationRequest!.StatusId);
        var order = await f.Order();
        Assert.False(KitchenOrderProgressPolicy.CanApply(order, OrderStatus.Ready));
        Assert.Equal(ErrorCode.OrderCancellationRequestPending,
            Assert.Throws<BusinessRuleException>(() => KitchenOrderProgressPolicy.Apply(order, OrderStatus.Ready, 4, DateTime.UtcNow)).Code);
        Assert.Equal(-2m, (await f.Base.Context.InventoryMovements.SingleAsync()).QuantityChange);
        Assert.Empty(f.Base.Context.Payments);
        Assert.Null(order.CancelledAt);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task KitchenDecisionIsFinalAuditedAndDoesNotRestoreStockOrIssuePayment(bool approved)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        var first = await f.Service.DecideAsync(1, f.Id, new(approved, "Mətbəx qərarı"));
        Assert.Equal(approved ? 2007 : 2003, first.StatusId);
        Assert.Equal(approved ? 3 : 4, first.CancellationRequest!.StatusId);
        Assert.False(first.CanDecideCancellationRequest);
        Assert.Equal(!approved, first.CanMarkReady);
        var replay = await f.Service.DecideAsync(1, f.Id, new(approved, "Başqa səbəb"));
        Assert.Equal(first.CancellationRequest, replay.CancellationRequest);
        Assert.Equal(first.CancellationRequest, (await f.Service.GetForKitchenAsync(1, f.Id)).CancellationRequest);
        Assert.Equal(ErrorCode.OrderCancellationRequestConflict,
            (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DecideAsync(1, f.Id, new(!approved, "Başqa qərar")))).Code);
        var order = await f.Order();
        Assert.Equal(approved, order.CancelledAt.HasValue);
        Assert.Equal(-2m, (await f.Base.Context.InventoryMovements.SingleAsync()).QuantityChange);
        Assert.Empty(f.Base.Context.Payments);
        Assert.Null((await f.Base.Context.Reservations.SingleAsync()).CancelledAt);
        var session = await f.Base.Context.TableSessions.Include(session => session.Orders).ThenInclude(order => order.OrderItems).SingleAsync(session => session.Id == 1);
        var bill = TableSessionBillingPolicy.Calculate(session);
        Assert.Equal(approved ? 0m : 7m, bill.TotalAmount);
        var action = approved ? AuditActions.OrderCancellationApproved : AuditActions.OrderCancellationRejected;
        Assert.Single(f.Base.Audit.Invocations, invocation => Equals(invocation.Arguments[1], action));
        Assert.Contains(f.Sent, notification => notification.UserId == 3);
        Assert.Contains(f.Sent, notification => notification.UserId == 1);
    }

    [Fact]
    public async Task CustomerCannotSkipWaiterAndRepeatedRequestKeepsOriginalReasonAndTime()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(3, RoleCode.Customer);
        var first = await f.Service.RequestForCustomerAsync(1, f.Id, new("Plan dəyişdi"));
        var repeated = await f.Service.RequestForCustomerAsync(1, f.Id, new("Başqa səbəb"));
        Assert.Equal(first.CancellationRequest, repeated.CancellationRequest);
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DecideAsync(1, f.Id, new(true, "Razıyam")));
        f.Actor(1, RoleCode.Waiter);
        var forwarded = await f.Service.ForwardAsync(1, 1, f.Id);
        var repeatedForward = await f.Service.ForwardAsync(1, 1, f.Id);
        Assert.Equal(forwarded.CancellationRequest, repeatedForward.CancellationRequest);
        Assert.Equal("Plan dəyişdi", forwarded.CancellationRequest!.Reason);
        Assert.Single(f.Base.Context.OrderCancellationRequests);
    }

    [Theory]
    [InlineData(2001)] [InlineData(2002)] [InlineData(2004)] [InlineData(2005)] [InlineData(2006)] [InlineData(2007)] [InlineData(2008)]
    public async Task OnlyActualPreparingOrderCanCreateRequest(int status)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Order()).StatusId = status;
        await f.Base.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Request());
        Assert.Empty(f.Base.Context.OrderCancellationRequests);
    }

    [Theory]
    [InlineData("pending")] [InlineData("review")] [InlineData("paidOrder")]
    [InlineData("closed")] [InlineData("ready")]
    public async Task StateAndFinancialGuardsAreRecheckedBeforeApproval(string state)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        if (state is "pending" or "review" or "paidOrder") f.Base.Context.Payments.Add(new Payment
        {
            Id = 9, RestaurantId = 1, TableSessionId = 1, OrderId = state == "paidOrder" ? f.Id : null,
            PaymentStatusId = state == "pending" ? 3001 : 3002, RequiresRefund = state == "review", Amount = 7
        });
        if (state == "closed") (await f.Base.Context.TableSessions.SingleAsync(session => session.Id == 1)).ClosedAt = DateTime.UtcNow;
        if (state == "ready") (await f.Order()).ReadyAt = DateTime.UtcNow;
        await f.Base.Context.SaveChangesAsync();
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DecideAsync(1, f.Id, new(true, "Razıyam")));
        Assert.Null((await f.Order()).CancelledAt);
        Assert.Equal(OrderCancellationRequestStatus.PendingKitchen, (await f.Order()).CancellationRequest!.StatusId);
    }

    [Theory]
    [InlineData(RoleCode.Waiter)] [InlineData(RoleCode.Manager)] [InlineData(RoleCode.Owner)] [InlineData(RoleCode.SuperAdmin)]
    public async Task OnlyLiveKitchenRoleCanDecide(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        (await f.Base.Context.UserRestaurants.SingleAsync(assignment => assignment.UserId == 4)).RoleId = (int)role;
        await f.Base.Context.SaveChangesAsync();
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DecideAsync(1, f.Id, new(true, "Razıyam")));
    }

    [Fact]
    public async Task ForeignCustomerAndReassignedWaiterCannotUseRequestOrForwardEndpoints()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(2, RoleCode.Customer);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.RequestForCustomerAsync(1, f.Id, new("Plan dəyişdi")));
        f.Actor(1, RoleCode.Waiter);
        await f.Request();
        (await f.Base.Context.TableSessions.SingleAsync(session => session.Id == 1)).WaiterUserId = 2;
        await f.Base.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.ForwardAsync(1, 1, f.Id));
    }

    [Fact]
    public async Task WaitingCustomerRequestExpiresIfOrderBecomesReadyBeforeForwarding()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(3, RoleCode.Customer);
        await f.Service.RequestForCustomerAsync(1, f.Id, new("Plan dəyişdi"));
        var order = await f.Order();
        Assert.True(KitchenOrderProgressPolicy.CanApply(order, OrderStatus.Ready));
        Assert.True(OrderCancellationRequestPolicy.ExpireAwaitingWaiter(order, DateTime.UtcNow, "Hazırdır"));
        KitchenOrderProgressPolicy.Apply(order, OrderStatus.Ready, 4, DateTime.UtcNow);
        await f.Base.Context.SaveChangesAsync();
        f.Actor(1, RoleCode.Waiter);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ForwardAsync(1, 1, f.Id));
        Assert.Equal(OrderCancellationRequestStatus.Expired, order.CancellationRequest!.StatusId);
    }

    [Theory]
    [InlineData("")] [InlineData("ab")]
    public async Task InvalidRequestAndDecisionReasonsDoNotCreateOrResolve(string reason)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RequestForWaiterAsync(1, 1, f.Id, new(reason)));
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DecideAsync(1, f.Id, new(true, reason)));
        Assert.Null((await f.Order()).CancelledAt);
    }

    [Theory]
    [InlineData("role")] [InlineData("closed")] [InlineData("ready")]
    public async Task StateAndRoleChangesWhileWaitingForLockAreRechecked(string state)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        f.Base.BeforeLock = async () =>
        {
            if (state == "role") (await f.Base.Context.UserRestaurants.SingleAsync(assignment => assignment.UserId == 4)).RoleId = (int)RoleCode.Manager;
            if (state == "closed") (await f.Base.Context.TableSessions.SingleAsync(session => session.Id == 1)).ClosedAt = DateTime.UtcNow;
            if (state == "ready") (await f.Order()).ReadyAt = DateTime.UtcNow;
            await f.Base.Context.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => f.Service.DecideAsync(1, f.Id, new(true, "Razıyam")));
        Assert.Null((await f.Order()).CancelledAt);
    }

    [Fact]
    public async Task MissingBooleanDecisionIsNotSilentlyTreatedAsRejection()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.DecideAsync(1, f.Id, new(null, "Qərar")));
        Assert.Null((await f.Order()).CancellationRequest!.ResolvedAt);
    }

    [Fact]
    public async Task RejectionCannotAskForIngredientReturn()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.DecideAsync(1, f.Id, new(false, "Hazırlıq gedir", true)));
        Assert.Null((await f.Order()).CancellationRequest!.ResolvedAt);
    }

    [Fact]
    public async Task LegacyApprovalCannotLaterBeReinterpretedAsUnusedIngredients()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Request();
        f.Actor(4, RoleCode.Kitchen);
        await f.Service.DecideAsync(1, f.Id, new(true, "İlkin qərar"));
        var request = (await f.Order()).CancellationRequest!;
        request.IngredientsUnused = null;
        await f.Base.Context.SaveChangesAsync();
        Assert.Equal(ErrorCode.OrderCancellationRequestConflict, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.DecideAsync(1, f.Id, new(true, "Başqa qərar", true)))).Code);
        Assert.Null(request.StockReturnedAt);
    }

    [Fact]
    public async Task CustomerRequestRequiresCurrentActiveWaiter()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Base.Context.UserRestaurants.SingleAsync(assignment => assignment.UserId == 1)).IsActive = false;
        await f.Base.Context.SaveChangesAsync();
        f.Actor(3, RoleCode.Customer);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RequestForCustomerAsync(1, f.Id, new("Plan dəyişdi")));
        Assert.Equal(ErrorCode.OrderCancellationWaiterUnavailable, error.Code);
        Assert.Empty(f.Base.Context.OrderCancellationRequests);
    }

    [Theory]
    [InlineData("az")] [InlineData("en")] [InlineData("ru")]
    public void RequestCatalogIncludesErrorsStatesAndNotifications(string language)
    {
        var bootstrap = new TranslationBootstrap();
        var messages = bootstrap.Snapshot.ForLanguage(language).Messages;
        foreach (var key in new[] { "api.error.OrderCancellationRequestNotAllowed", "api.error.OrderCancellationRequestConflict",
                     "api.error.OrderCancellationRequestPending", "api.error.OrderCancellationWaiterUnavailable", "api.error.OrderCancellationRequestNotFound",
                     "ui.cancellationRequest.status.approved", "ui.cancellationRequest.status.rejected", "ui.cancellationRequest.status.expired" })
            Assert.False(string.IsNullOrWhiteSpace(messages[key]));
        var match = bootstrap.NotificationSourceMatcher.Match("Mətbəx #81 sifarişinin ləğvini təsdiqlədi.");
        Assert.Equal("notification.orderCancellationRequest.approved", match!.Key);
        Assert.Equal("81", match.Arguments["orderId"]);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public BaseFixture Base { get; }
        public int Id { get; }
        public OrderCancellationRequestManager Service { get; }
        public List<CreateNotificationRequest> Sent { get; } = [];

        /// <summary>Həqiqi repository-lər, ayrıca transaction mock-u və mövcud təhlükəsiz sifariş fixture-i ilə xidmət yaradır.</summary>
        private Fixture(BaseFixture source, int id)
        {
            Base = source; Id = id;
            var tables = new Mock<ITableRepository>();
            tables.Setup(table => table.AcquireReservationLockAsync(1, 1, It.IsAny<CancellationToken>()))
                .Returns(async () => { if (source.BeforeLock != null) await source.BeforeLock(); });
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(factory => factory.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(source.Transaction.Object);
            var notifications = new Mock<INotificationService>();
            notifications.Setup(service => service.CreateAsync(It.IsAny<CreateNotificationRequest>())).Callback<CreateNotificationRequest>(Sent.Add).Returns(Task.CompletedTask);
            Service = new OrderCancellationRequestManager(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == source.Http),
                Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), new OrderRepository(source.Context), new TableSessionRepository(source.Context),
                tables.Object, new UserRepository(source.Context), new UserRestaurantRepository(source.Context), transactions.Object,
                source.Audit.Object, notifications.Object, TimeProvider.System, Mock.Of<ECafe.Application.Services.Orders.Abstract.IOrderStockReturnService>());
        }

        /// <summary>Hazırlanan sifariş, öz müştərisi, cari ofisiantı və aktiv mətbəxi olan təcrid edilmiş baza qurur.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var source = await BaseFixture.CreateAsync();
            var created = await source.CreateAsync(new() { Items = [new() { ItemId = 1, Quantity = 2 }] });
            var order = await source.Context.Orders.SingleAsync();
            order.StatusId = 2003;
            order.SentToKitchenAt = order.AcceptedAt = order.PreparingAt = DateTime.UtcNow.AddMinutes(-5);
            source.Context.Users.Add(new User { Id = 4, Name = "Kitchen", Surname = "Test", Email = "kitchen@example.test", Phone = "4", Password = "test", IsActive = true });
            source.Context.UserRestaurants.Add(new UserRestaurant { Id = 2, RestaurantId = 1, UserId = 4, RoleId = (int)RoleCode.Kitchen, IsActive = true });
            source.Context.InventoryMovements.Add(new InventoryMovement { Id = 1, RestaurantId = 1, RelatedOrderId = order.Id,
                MovementTypeId = (int)InventoryMovementTypeCode.OrderConsumption, QuantityChange = -2 });
            await source.Context.SaveChangesAsync();
            source.Audit.Invocations.Clear();
            return new(source, created.Order.Id);
        }

        /// <summary>Token kontekstini dəyişir; DB-dəki canlı təyinat ayrıca yoxlanılır.</summary>
        public void Actor(int id, RoleCode role) => Base.Http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", id.ToString()), new Claim("restaurantId", "1"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));
        /// <summary>Cari ofisiantın birbaşa mətbəxə sorğusunu çağırır.</summary>
        public Task<SessionOrderResponse> Request() => Service.RequestForWaiterAsync(1, 1, Id, new("Plan dəyişdi"));
        /// <summary>Yazıdan sonra request navigation ilə saxlanmış sifarişi oxuyur.</summary>
        public Task<Order> Order() => Base.Context.Orders.Include(order => order.CancellationRequest).SingleAsync();
        /// <summary>Təcrid edilmiş EF kontekstini bağlayır.</summary>
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }
}
