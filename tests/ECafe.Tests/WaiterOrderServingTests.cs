using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Api.Swagger;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Fixture = ECafe.Tests.WaiterOrderCreationTests.Fixture;

namespace ECafe.Tests;

public sealed class WaiterOrderServingTests
{
    [Fact]
    public async Task ReadyOrderBecomesServedOnceAndPreservesPriceItemsSessionAndKitchenTimes()
    {
        await using var f = await Fixture.CreateAsync();
        var orderId = await PrepareReadyAsync(f);
        var before = await f.Manager.GetByIdAsync(1, 1, orderId);
        Assert.True(before.CanMarkServed);
        object? auditPayload = null;
        f.Audit.Setup(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderServed, It.IsAny<object>(),
            AuditEntityTypes.Order, orderId, It.IsAny<string>())).Callback<int, string, object?, string?, long?, string?>((_, _, payload, _, _, _) => auditPayload = payload)
            .Returns(Task.CompletedTask);
        var result = await f.Manager.MarkServedAsync(1, 1, orderId);
        Assert.Equal(2005, result.StatusId);
        Assert.NotNull(result.ServedAt);
        Assert.Equal(TimeSpan.Zero, result.ServedAt.Value.Offset);
        Assert.False(result.CanMarkServed);
        Assert.Equal(before.Subtotal, result.Subtotal);
        Assert.Equal(before.Items, result.Items);
        Assert.Equal(before.SentToKitchenAt, result.SentToKitchenAt);
        var session = await f.Context.TableSessions.SingleAsync(s => s.Id == 1);
        Assert.Equal(7001, session.StatusId);
        Assert.Null(session.ClosedAt);
        var order = await f.Context.Orders.SingleAsync(o => o.Id == orderId);
        Assert.NotNull(order.ReadyAt);
        Assert.Null(order.ClosedAt);
        Assert.Empty(await f.Context.Payments.ToListAsync());
        Assert.Empty(await f.Context.InventoryMovements.ToListAsync());
        var json = JsonSerializer.Serialize(auditPayload);
        Assert.Contains("\"servedByUserId\":1", json);
        Assert.Contains("\"toStatusId\":2005", json);
        var replay = await f.Manager.MarkServedAsync(1, 1, orderId);
        Assert.Equal(result.ServedAt, replay.ServedAt);
        f.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderServed, It.IsAny<object>(),
            AuditEntityTypes.Order, orderId, It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(2001)]
    [InlineData(2002)]
    [InlineData(2003)]
    [InlineData(2005)] // Invalid served record without timestamp is not a replay.
    [InlineData(2006)]
    [InlineData(2007)]
    [InlineData(2008)]
    public async Task OnlyReadyMayTransition(int statusId)
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        (await f.Context.Orders.SingleAsync(o => o.Id == id)).StatusId = statusId;
        await f.Context.SaveChangesAsync();
        Assert.False((await f.Manager.GetByIdAsync(1, 1, id)).CanMarkServed);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Manager.MarkServedAsync(1, 1, id));
        Assert.Equal(ErrorCode.OrderCannotMarkServed, error.Code);
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("unsent")]
    [InlineData("notReady")]
    [InlineData("closed")]
    [InlineData("servedTime")]
    [InlineData("empty")]
    [InlineData("negative")]
    public async Task InvalidReadyStateHasNoServeAction(string state)
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var order = await f.Context.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == id);
        if (state == "unsent") order.SentToKitchenAt = null;
        if (state == "notReady") order.ReadyAt = null;
        if (state == "closed") order.ClosedAt = DateTime.UtcNow;
        if (state == "servedTime") order.ServedAt = DateTime.UtcNow;
        if (state == "empty") foreach (var line in order.OrderItems) line.IsDeleted = true;
        if (state == "negative") order.OrderItems.First().Quantity = 0;
        await f.Context.SaveChangesAsync();
        Assert.False((await f.Manager.GetByIdAsync(1, 1, id)).CanMarkServed);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Manager.MarkServedAsync(1, 1, id));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("restaurant")]
    [InlineData("deleted")]
    [InlineData("table")]
    [InlineData("missing")]
    public async Task OrderIdsCannotBypassSessionOrTenantBoundary(string state)
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var order = await f.Context.Orders.SingleAsync(o => o.Id == id);
        if (state == "session") order.TableSessionId = 2;
        if (state == "restaurant") order.RestaurantId = 2;
        if (state == "deleted") order.IsDeleted = true;
        if (state == "table") order.TableId = 2;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.MarkServedAsync(1, 1, state == "missing" ? 999 : id));
    }

    [Theory]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task LiveRoleMustRemainWaiterDespiteOldToken(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        (await f.Context.UserRestaurants.SingleAsync()).RoleId = (int)role;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.MarkServedAsync(1, 1, id));
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("user")]
    [InlineData("closedSession")]
    [InlineData("reassigned")]
    [InlineData("movedTable")]
    [InlineData("restaurant")]
    public async Task PermissionAndSessionAreRecheckedAfterWaitingForTableLock(string change)
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        f.BeforeLock = async () =>
        {
            if (change == "assignment") (await f.Context.UserRestaurants.SingleAsync()).IsActive = false;
            if (change == "user") (await f.Context.Users.SingleAsync(u => u.Id == 1)).IsActive = false;
            if (change == "restaurant") (await f.Context.Restaurants.SingleAsync(r => r.Id == 1)).IsActive = false;
            var session = await f.Context.TableSessions.SingleAsync(s => s.Id == 1);
            if (change == "closedSession") { session.StatusId = 7002; session.ClosedAt = DateTime.UtcNow; }
            if (change == "reassigned") session.WaiterUserId = 2;
            if (change == "movedTable") session.TableId = 2;
            await f.Context.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => f.Manager.MarkServedAsync(1, 1, id));
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await f.Context.Orders.SingleAsync(o => o.Id == id)).ServedAt);
    }

    [Fact]
    public async Task CurrentWaiterCanServeOrderCreatedByPreviousWaiterOrCustomerSource()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var order = await f.Context.Orders.SingleAsync(o => o.Id == id);
        order.WaiterUserId = 2;
        order.SourceId = (int)OrderSource.Customer;
        await f.Context.SaveChangesAsync();
        Assert.Equal(2005, (await f.Manager.MarkServedAsync(1, 1, id)).StatusId);
    }

    [Fact]
    public async Task ParallelRetriesWriteOneServingAuditAndSameTimestamp()
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1);
        await using var first = await Fixture.CreateAsync(store, gate);
        var id = await PrepareReadyAsync(first);
        first.Context.ChangeTracker.Clear();
        await using var second = await Fixture.CreateAsync(store, gate);
        var results = await Task.WhenAll(first.Manager.MarkServedAsync(1, 1, id), second.Manager.MarkServedAsync(1, 1, id));
        Assert.All(results, result => Assert.Equal(2005, result.StatusId));
        Assert.Equal(results[0].ServedAt, results[1].ServedAt);
        var writes = first.Audit.Invocations.Concat(second.Audit.Invocations)
            .Count(call => call.Method.Name == "RecordRestaurantActionAsync" && (string)call.Arguments[1] == AuditActions.OrderServed);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task OriginalWaiterLosesReplayAccessAfterHandoffButNewOwnerDoesNotRewriteMilestone()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var served = await f.Manager.MarkServedAsync(1, 1, id);
        (await f.Context.TableSessions.SingleAsync(s => s.Id == 1)).WaiterUserId = 2;
        f.Context.UserRestaurants.Add(new UserRestaurant { Id = 2, RestaurantId = 1, UserId = 2, RoleId = 4, IsActive = true });
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.MarkServedAsync(1, 1, id));
        f.Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "2"), new Claim("restaurantId", "1"),
            new Claim(ClaimTypes.Role, "4")], "test"));
        Assert.Equal(served.ServedAt, (await f.Manager.MarkServedAsync(1, 1, id)).ServedAt);
        f.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderServed, It.IsAny<object>(),
            AuditEntityTypes.Order, id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CustomerTrackingSeesActualServedTimeWithoutBeingMarkedPaidOrClosed()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var served = await f.Manager.MarkServedAsync(1, 1, id);
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", "3"), new Claim(ClaimTypes.Role, "5")], "test")) };
        var tracking = new CustomerOrderTrackingManager(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http),
            Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), new TableSessionRepository(f.Context),
            new OrderRepository(f.Context), new UserRepository(f.Context));
        var detail = await tracking.GetOrderAsync(1, id);
        Assert.Equal(2005, detail.StatusId);
        Assert.Equal(served.ServedAt, detail.ServedAt);
        Assert.Null(detail.ClosedAt);
    }

    [Fact]
    public async Task ServedOrderLeavesKitchenQueueButStaysInWaiterSessionHistory()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        var waiterClaims = f.Http.User;
        f.Context.UserRestaurants.Add(new UserRestaurant { Id = 2, UserId = 2, RestaurantId = 1, RoleId = 6, IsActive = true });
        await f.Context.SaveChangesAsync();
        f.Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "2"), new Claim("restaurantId", "1"),
            new Claim(ClaimTypes.Role, "6")], "test"));
        var kitchen = new KitchenOrderManager(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == f.Http),
            Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), new OrderRepository(f.Context),
            Mock.Of<ITableSessionService>(), Mock.Of<ITableRepository>(), new UserRestaurantRepository(f.Context),
            Mock.Of<IApplicationDbTransactionFactory>(), Mock.Of<INotificationService>(), f.Audit.Object, TimeProvider.System,
            Mock.Of<ECafe.Application.Services.Orders.Abstract.IOrderStockService>());
        Assert.Equal(id, Assert.Single((await kitchen.GetQueueAsync(1, new PaginationFilter())).Items).Id);
        f.Http.User = waiterClaims;
        await f.Manager.MarkServedAsync(1, 1, id);
        var detail = Assert.Single((await f.Manager.GetForSessionAsync(1, 1, new PaginationFilter())).Items);
        Assert.Equal(2005, detail.StatusId);
        Assert.NotNull(detail.ServedAt);
        Assert.False(detail.CanMarkServed);
        f.Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "2"), new Claim("restaurantId", "1"),
            new Claim(ClaimTypes.Role, "6")], "test"));
        Assert.Empty((await kitchen.GetQueueAsync(1, new PaginationFilter())).Items);
    }

    [Fact]
    public async Task AuditFailureDoesNotCommitAndCancellationDoesNotServe()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await PrepareReadyAsync(f);
        f.Audit.Setup(a => a.RecordRestaurantActionAsync(1, AuditActions.OrderServed, It.IsAny<object>(),
            AuditEntityTypes.Order, id, It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Manager.MarkServedAsync(1, 1, id));
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        await using var cancelled = await Fixture.CreateAsync();
        var otherId = await PrepareReadyAsync(cancelled);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.Manager.MarkServedAsync(1, 1, otherId, source.Token));
        Assert.Null((await cancelled.Context.Orders.SingleAsync(o => o.Id == otherId)).ServedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidIdentifiersAndForeignRestaurantAreRejected(int id)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.MarkServedAsync(1, 1, id));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.MarkServedAsync(1, id, 1));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.MarkServedAsync(id, 1, 1));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.MarkServedAsync(2, 1, 1));
    }

    [Fact]
    public void EndpointIsExplicitAndDoesNotAcceptActorStatusPaymentOrBody()
    {
        var controller = typeof(OrderFlowController);
        Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(), a => a.GetType() == typeof(AuthorizeAttribute));
        Assert.NotNull(controller.GetCustomAttribute<HasPermissionAttribute>());
        var method = controller.GetMethod(nameof(OrderFlowController.MarkServed))!;
        Assert.Null(method.GetCustomAttribute<RequireActiveRestaurantContractAttribute>());
        Assert.Equal("api/v1/restaurants/{restaurantId:int}/table-sessions/{tableSessionId:int}/orders/{orderId:int}/serve",
            method.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.DoesNotContain(method.GetParameters(), p => p.GetCustomAttribute<FromBodyAttribute>() != null);
        Assert.Contains("OrderFlow.MarkServed", EcafeSwaggerMetadata.Endpoints.Keys);
        Assert.Contains("hazır sifariş", new ErrorMessageProvider().GetMessage(ErrorCode.OrderCannotMarkServed));
    }

    /// <summary>Yaradılma testlərinin həqiqi servisi ilə sifariş yaradır, sonra mətbəxin tamamladığı hazır vəziyyəti qurur.</summary>
    private static async Task<int> PrepareReadyAsync(Fixture f)
    {
        var created = await f.CreateAsync(new CreateWaiterOrderRequest { Items = [new CreateOrderLineRequest { ItemId = 1, Quantity = 2 }] });
        var order = await f.Context.Orders.SingleAsync(o => o.Id == created.Order.Id);
        order.StatusId = 2004;
        order.SentToKitchenAt = DateTime.UtcNow.AddMinutes(-3);
        order.AcceptedAt = DateTime.UtcNow.AddMinutes(-2);
        order.PreparingAt = DateTime.UtcNow.AddMinutes(-1);
        order.ReadyAt = DateTime.UtcNow;
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        f.Audit.Invocations.Clear();
        f.Transaction.Invocations.Clear();
        return order.Id;
    }
}
