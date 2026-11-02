using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed class CustomerOrderSubmissionTests
{
    [Fact]
    public async Task Customer_creates_pending_order_with_server_snapshots_without_kitchen_or_payment()
    {
        await using var f = await CustomerAsync();
        var result = await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        Assert.False(result.IsReplay);
        Assert.Equal(2009, result.Order.StatusId);
        Assert.Null(result.Order.SentToKitchenAt);
        Assert.True(result.Order.CanCancel);
        Assert.Equal(7m, result.Order.Subtotal);
        var order = await f.Context.Orders.SingleAsync();
        Assert.Equal((int)OrderSource.Customer, order.SourceId);
        Assert.Equal(3, order.CustomerUserId);
        Assert.Equal(1, order.WaiterUserId);
        Assert.Empty(await f.Context.Payments.ToListAsync());
    }

    [Fact]
    public async Task Same_key_replays_but_new_intent_creates_another_order()
    {
        await using var f = await CustomerAsync();
        var key = Guid.NewGuid();
        var first = await f.Manager.CreateAsync(1, key, Request());
        var replay = await f.Manager.CreateAsync(1, key, Request());
        Assert.True(replay.IsReplay);
        Assert.Equal(first.Order.Id, replay.Order.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Manager.CreateAsync(1, key, Request(3)));
        await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        Assert.Equal(2, await f.Context.Orders.CountAsync());
    }

    [Fact]
    public async Task Closed_foreign_and_inactive_sessions_cannot_create()
    {
        await using var f = await CustomerAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.CreateAsync(4, Guid.NewGuid(), Request()));
        SetActor(f, 2, RoleCode.Customer);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.CreateAsync(1, Guid.NewGuid(), Request()));
        SetActor(f, 3, RoleCode.Waiter);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.CreateAsync(1, Guid.NewGuid(), Request()));
        SetActor(f, 3, RoleCode.Customer);
        var customer = await f.Context.Users.SingleAsync(user => user.Id == 3);
        customer.IsActive = false;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.CreateAsync(1, Guid.NewGuid(), Request()));
        Assert.Empty(await f.Context.Orders.ToListAsync());
    }

    [Fact]
    public async Task Session_is_rechecked_after_table_lock()
    {
        await using var f = await CustomerAsync();
        f.BeforeLock = async () =>
        {
            var session = await f.Context.TableSessions.SingleAsync(session => session.Id == 1);
            session.StatusId = StatusIds.TableSession(TableSessionStatus.Closed);
            session.ClosedAt = DateTime.UtcNow;
            await f.Context.SaveChangesAsync();
        };
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.CreateAsync(1, Guid.NewGuid(), Request()));
        Assert.Empty(await f.Context.Orders.ToListAsync());
    }

    [Theory]
    [InlineData("contract")]
    [InlineData("waiter")]
    [InlineData("pending-payment")]
    [InlineData("refund-review")]
    public async Task Capability_and_submission_share_financial_and_assignment_guards(string blockedBy)
    {
        await using var f = await CustomerAsync();
        if (blockedBy == "contract") (await f.Context.RestaurantContracts.SingleAsync()).EndDate = DateTime.UtcNow.AddMinutes(-1);
        if (blockedBy == "waiter") (await f.Context.UserRestaurants.SingleAsync()).IsActive = false;
        if (blockedBy is "pending-payment" or "refund-review")
            f.Context.Payments.Add(new Payment { RestaurantId = 1, TableSessionId = 1, Amount = 7,
                PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending),
                RequiresRefund = blockedBy == "refund-review" });
        await f.Context.SaveChangesAsync();
        var session = await CustomerOrderTrackingMapper.ProjectSessions(f.Context.TableSessions.Where(session => session.Id == 1)).SingleAsync();
        Assert.False(session.CanCreateOrder);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.CreateAsync(1, Guid.NewGuid(), Request()));
    }

    [Fact]
    public async Task Pending_is_not_charged_but_blocks_checkout_and_can_be_cancelled()
    {
        await using var f = await CustomerAsync();
        await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        var order = await f.Context.Orders.Include(order => order.OrderItems).Include(order => order.TableSession).SingleAsync();
        var bill = TableSessionBillingPolicy.Calculate(order.TableSession!);
        Assert.Equal(0, bill.TotalAmount);
        Assert.True(bill.HasUnservedOrders);
        Assert.Throws<BusinessRuleException>(() => TableSessionBillingPolicy.EnsureCanClose(order.TableSession!));
        Assert.True(OrderCancellationPolicy.CanCancel(order));
        Assert.True(OrderCancellationPolicy.Apply(order, 3, DateTime.UtcNow, "Customer changed their mind"));
        Assert.False(TableSessionBillingPolicy.Calculate(order.TableSession!).HasUnservedOrders);
    }

    [Fact]
    public async Task Current_waiter_approves_and_sends_atomically_and_replay_does_not_change_timestamp()
    {
        await using var f = await CustomerAsync();
        var created = await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        var sender = Sender(f);
        var sent = await sender.SendAsync(1, 1, created.Order.Id);
        Assert.Equal(2001, sent.StatusId);
        Assert.Equal((int)OrderSource.Customer, sent.SourceId);
        Assert.NotNull(sent.SentToKitchenAt);
        var replay = await sender.SendAsync(1, 1, created.Order.Id);
        Assert.Equal(sent.SentToKitchenAt, replay.SentToKitchenAt);
        Assert.Equal(1, await f.Context.Orders.CountAsync());
        Assert.Equal(7, TableSessionBillingPolicy.Calculate(await f.Context.TableSessions.Include(session => session.Orders)
            .ThenInclude(order => order.OrderItems).SingleAsync(session => session.Id == 1)).TotalAmount);
    }

    [Fact]
    public async Task Cancelled_customer_order_cannot_be_approved()
    {
        await using var f = await CustomerAsync();
        var created = await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        var order = await f.Context.Orders.SingleAsync();
        order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        order.CancelledAt = DateTime.UtcNow;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Sender(f).SendAsync(1, 1, created.Order.Id));
        Assert.Null((await f.Context.Orders.SingleAsync()).SentToKitchenAt);
    }

    [Fact]
    public async Task Product_removed_from_sale_after_submission_cannot_be_approved()
    {
        await using var f = await CustomerAsync();
        var created = await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        (await f.Context.Items.SingleAsync(item => item.Id == 1)).IsAvailable = false;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Sender(f).SendAsync(1, 1, created.Order.Id));
        Assert.Null((await f.Context.Orders.SingleAsync()).SentToKitchenAt);
    }

    [Fact]
    public async Task Old_waiter_cannot_approve_after_reassignment()
    {
        await using var f = await CustomerAsync();
        var created = await f.Manager.CreateAsync(1, Guid.NewGuid(), Request());
        (await f.Context.TableSessions.SingleAsync(session => session.Id == 1)).WaiterUserId = 2;
        f.Context.UserRestaurants.Add(new UserRestaurant { UserId = 2, RestaurantId = 1, RoleId = 4, IsActive = true });
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => Sender(f).SendAsync(1, 1, created.Order.Id));
        var sent = await Sender(f, 2).SendAsync(1, 1, created.Order.Id);
        Assert.Equal(2, sent.WaiterUserId);
    }

    [Fact]
    public void Customer_endpoint_is_role_protected_and_does_not_accept_owner_or_price_fields()
    {
        Assert.Equal("5", Assert.Single(typeof(CustomerOrderController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()).Roles);
        Assert.Null(typeof(CreateCustomerOrderRequest).GetProperty("CustomerUserId"));
        Assert.Null(typeof(CreateCustomerOrderRequest).GetProperty("RestaurantId"));
        Assert.Null(typeof(CreateOrderLineRequest).GetProperty("UnitPrice"));
    }

    /// <summary>Mövcud real repository test qurğusunda aktiv müqaviləli müştəri profili yaradır.</summary>
    private static async Task<WaiterOrderCreationTests.Fixture> CustomerAsync()
    {
        var f = await WaiterOrderCreationTests.Fixture.CreateAsync();
        f.Context.RestaurantContracts.Add(new RestaurantContract { RestaurantId = 1, ContractNumber = "customer-test",
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
            StatusId = StatusIds.Contract(ContractStatus.Active) });
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        SetActor(f, 3, RoleCode.Customer);
        return f;
    }

    /// <summary>İstifadəçi kontekstini dəyişərək müştəri və cari ofisiant səlahiyyətlərini ayrı yoxlayır.</summary>
    private static void SetActor(WaiterOrderCreationTests.Fixture f, int id, RoleCode role)
        => f.Http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", id.ToString()), new Claim("restaurantId", "1"),
            new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));

    /// <summary>Hazırlama və ödənişi əvəz etmədən yalnız transaction sərhədini mock edən ofisiant göndərmə xidmətini qurur.</summary>
    private static KitchenOrderManager Sender(WaiterOrderCreationTests.Fixture f, int waiterId = 1)
    {
        SetActor(f, waiterId, RoleCode.Waiter);
        var accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == f.Http);
        var config = new ConfigurationBuilder().Build();
        var sessions = new TableSessionRepository(f.Context);
        var assignments = new UserRestaurantRepository(f.Context);
        var access = new TableSessionManager(accessor, Mock.Of<IMapper>(), config, sessions, assignments);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(factory => factory.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IApplicationDbTransaction>());
        var tables = new Mock<ITableRepository>();
        tables.Setup(table => table.AcquireReservationLockAsync(1, 1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return new KitchenOrderManager(accessor, Mock.Of<IMapper>(), config, new OrderRepository(f.Context), access,
            tables.Object, assignments, transactions.Object, Mock.Of<INotificationService>(), f.Audit.Object,
            TimeProvider.System, Mock.Of<IOrderStockService>());
    }

    /// <summary>Eyni məhsulun miqdarını dəyişərək yeni niyyət və ziddiyyətli təkrar sınaqlarını qurur.</summary>
    private static CreateCustomerOrderRequest Request(int quantity = 2)
        => new() { Items = [new() { ItemId = 1, Quantity = quantity, ExpectedUnitPrice = 3.5m }] };
}
