using System.Reflection;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Fixture = ECafe.Tests.KitchenOrderTests.Fixture;

namespace ECafe.Tests;

public sealed class KitchenOrderProgressTests
{
    [Fact]
    public async Task FullProgressRecordsActorsTimesAuditAndExactlyOneResponsibleWaiterNotification()
    {
        await using var f = await SentFixtureAsync();
        var accepted = await f.Service.AcceptAsync(1, 1);
        Assert.Equal(2002, accepted.StatusId);
        Assert.Equal(2, accepted.AcceptedByUserId);
        Assert.NotNull(accepted.AcceptedAt);
        Assert.False(accepted.CanAccept);
        Assert.True(accepted.CanStartPreparing);
        Assert.False(accepted.CanMarkReady);
        var preparing = await f.Service.StartPreparingAsync(1, 1);
        Assert.Equal(2003, preparing.StatusId);
        Assert.Equal(2, preparing.PreparingByUserId);
        Assert.NotNull(preparing.PreparingAt);
        Assert.True(preparing.CanMarkReady);
        var ready = await f.Service.MarkReadyAsync(1, 1);
        Assert.Equal(2004, ready.StatusId);
        Assert.Equal(2, ready.ReadyByUserId);
        Assert.NotNull(ready.ReadyAt);
        Assert.True(ready.AcceptedAt <= ready.PreparingAt && ready.PreparingAt <= ready.ReadyAt);
        Assert.False(ready.CanAccept || ready.CanStartPreparing || ready.CanMarkReady);
        var notice = Assert.Single(f.Sent);
        Assert.Equal((int)NotificationType.OrderReady, notice.TypeId);
        Assert.Equal(1, notice.UserId);
        Assert.Equal(1, notice.RestaurantId);
        Assert.Equal("Order", notice.RelatedEntityType);
        Assert.Contains("\"section\":\"order-ready\"", notice.PayloadJson);
        Assert.Equal(3, f.Audit.Invocations.Count);
        foreach (var action in new[] { AuditActions.OrderAcceptedByKitchen, AuditActions.OrderPreparationStarted, AuditActions.OrderMarkedReady })
            Assert.Contains(f.Audit.Invocations, call => Equals(call.Arguments[1], action));
        Assert.Empty(await f.Db.Payments.ToListAsync());
        Assert.Empty(await f.Db.InventoryMovements.ToListAsync());
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).ClosedAt);
        Assert.Contains((await f.Service.GetQueueAsync(1, new PaginationFilter())).Items, row => row.Id == 1 && row.ReadyAt != null);
    }

    [Fact]
    public async Task RetriesPreserveTheFirstDecisionAndNeverRewindAnAdvancedStatus()
    {
        await using var f = await SentFixtureAsync();
        var first = await f.Service.AcceptAsync(1, 1);
        Assert.Equal(first.AcceptedAt, (await f.Service.AcceptAsync(1, 1)).AcceptedAt);
        var start = await f.Service.StartPreparingAsync(1, 1);
        Assert.Equal(start.PreparingAt, (await f.Service.StartPreparingAsync(1, 1)).PreparingAt);
        var ready = await f.Service.MarkReadyAsync(1, 1);
        Assert.Equal(ready.ReadyAt, (await f.Service.MarkReadyAsync(1, 1)).ReadyAt);
        Assert.Equal(2004, (await f.Service.AcceptAsync(1, 1)).StatusId);
        Assert.Equal(2004, (await f.Service.StartPreparingAsync(1, 1)).StatusId);
        Assert.Single(f.Sent);
        Assert.Equal(3, f.Audit.Invocations.Count);
    }

    [Fact]
    public async Task AnotherKitchenEmployeeCanContinueButCannotOverwriteTheSameCompletedStep()
    {
        await using var f = await SentFixtureAsync();
        await f.Service.AcceptAsync(1, 1);
        await MakeSecondKitchenAsync(f);
        f.LoginKitchen(3);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, 1));
        var started = await f.Service.StartPreparingAsync(1, 1);
        Assert.Equal(2, started.AcceptedByUserId);
        Assert.Equal(3, started.PreparingByUserId);
        f.LoginKitchen();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.StartPreparingAsync(1, 1));
        var ready = await f.Service.MarkReadyAsync(1, 1);
        Assert.Equal(2, ready.ReadyByUserId);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("ready")]
    public async Task UnacceptedOrderCannotSkipSteps(string action)
    {
        await using var f = await SentFixtureAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => action == "start"
            ? f.Service.StartPreparingAsync(1, 1) : f.Service.MarkReadyAsync(1, 1));
        Assert.Equal(ErrorCode.KitchenOrderTransitionNotAllowed, error.Code);
        Assert.Empty(f.Sent);
        Assert.Empty(f.Audit.Invocations);
    }

    [Fact]
    public async Task AcceptedOrderCannotSkipPreparingAndUnsentOrderCannotBeAccepted()
    {
        await using var f = await Fixture.CreateAsync();
        f.LoginKitchen();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, 1));
        var order = await f.Db.Orders.SingleAsync(o => o.Id == 1);
        order.SentToKitchenAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        await f.Service.AcceptAsync(1, 1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.MarkReadyAsync(1, 1));
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData(2005)]
    [InlineData(2006)]
    [InlineData(2007)]
    [InlineData(2008)]
    public async Task TerminalAndScheduledOrdersCannotProgress(int statusId)
    {
        await using var f = await SentFixtureAsync();
        (await f.Db.Orders.SingleAsync(o => o.Id == 1)).StatusId = statusId;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, 1));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.MarkReadyAsync(1, 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(999)]
    public async Task OtherRestaurantDeletedAndMissingOrdersReturnTheSameNotFound(int orderId)
    {
        await using var f = await SentFixtureAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => f.Service.AcceptAsync(1, orderId));
        Assert.Equal(ErrorCode.KitchenOrderNotFound, error.Code);
    }

    [Theory]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task LiveDatabaseRoleIsRequiredDespiteAStaleKitchenToken(RoleCode role)
    {
        await using var f = await SentFixtureAsync();
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).RoleId = (int)role;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.AcceptAsync(1, 1));
        Assert.Empty(f.Sent);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("inactive")]
    [InlineData("cancelled")]
    [InlineData("closed")]
    [InlineData("table")]
    [InlineData("deleted")]
    [InlineData("unsent")]
    public async Task ChangesWhileWaitingForTheTableLockAreRechecked(string change)
    {
        await using var f = await SentFixtureAsync();
        f.BeforeLock = async () =>
        {
            var order = await f.Db.Orders.SingleAsync(o => o.Id == 1);
            if (change == "role") (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).RoleId = 4;
            if (change == "inactive") (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 2)).IsActive = false;
            if (change == "cancelled") order.StatusId = 2007;
            if (change == "closed") order.ClosedAt = DateTime.UtcNow;
            if (change == "deleted") order.IsDeleted = true;
            if (change == "unsent") order.SentToKitchenAt = null;
            if (change == "table") order.TableId = 2;
            await f.Db.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => f.Service.AcceptAsync(1, 1));
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(f.Audit.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelAcceptHonorsTheFirstActorAndRecordsTheStepOnlyOnce(bool differentActors)
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1, 1);
        await using var a = await SentFixtureAsync(store, gate);
        await MakeSecondKitchenAsync(a);
        a.Db.ChangeTracker.Clear();
        await using var b = await Fixture.CreateAsync(store, gate);
        b.LoginKitchen(differentActors ? 3 : 2);
        var first = CaptureAcceptAsync(a);
        var second = CaptureAcceptAsync(b);
        var results = await Task.WhenAll(first, second);
        Assert.Equal(differentActors ? 1 : 2, results.Count(result => result == null));
        if (differentActors) Assert.IsType<BusinessRuleException>(Assert.Single(results, result => result != null));
        Assert.Equal(1, a.Audit.Invocations.Count + b.Audit.Invocations.Count);
        Assert.Equal(2002, (await a.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == 1)).StatusId);
    }

    [Fact]
    public async Task ParallelReadyNotifiesTheResponsibleWaiterOnce()
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1, 1);
        await using var a = await SentFixtureAsync(store, gate);
        await a.Service.AcceptAsync(1, 1);
        await a.Service.StartPreparingAsync(1, 1);
        a.Audit.Invocations.Clear();
        a.Db.ChangeTracker.Clear();
        await using var b = await Fixture.CreateAsync(store, gate);
        b.LoginKitchen();
        var results = await Task.WhenAll(a.Service.MarkReadyAsync(1, 1), b.Service.MarkReadyAsync(1, 1));
        Assert.Equal(results[0].ReadyAt, results[1].ReadyAt);
        Assert.Equal(1, a.Sent.Count + b.Sent.Count);
        Assert.Equal(1, a.Audit.Invocations.Count + b.Audit.Invocations.Count);
    }

    [Fact]
    public async Task OneKitchenEmployeeCanAcceptMultipleTablesWithoutClosingTheFirstOrder()
    {
        await using var f = await SentFixtureAsync();
        f.Db.Tables.Add(new Table { Id = 3, RestaurantId = 1, TableNo = 3, IsActive = true, Capacity = 4 });
        f.Db.Orders.Add(new Order { Id = 20, RestaurantId = 1, TableId = 3, StatusId = 2001,
            OpenedAt = DateTime.UtcNow, SentToKitchenAt = DateTime.UtcNow,
            OrderItems = [new OrderItem { ItemId = 1, NameSnapshot = "Plov", Quantity = 1 }] });
        await f.Db.SaveChangesAsync();
        Assert.Equal(2002, (await f.Service.AcceptAsync(1, 1)).StatusId);
        Assert.Equal(2002, (await f.Service.AcceptAsync(1, 20)).StatusId);
        Assert.Equal(2, await f.Db.Orders.CountAsync(o => o.AcceptedByUserId == 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadyUsesCurrentSessionWaiterAndDoesNotNotifyAnInactiveWaiter(bool inactive)
    {
        await using var f = await SentFixtureAsync();
        (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).WaiterUserId = 3;
        (await f.Db.UserRestaurants.SingleAsync(a => a.Id == 3)).IsActive = !inactive;
        await f.Db.SaveChangesAsync();
        await f.Service.AcceptAsync(1, 1);
        await f.Service.StartPreparingAsync(1, 1);
        await f.Service.MarkReadyAsync(1, 1);
        if (inactive) Assert.Empty(f.Sent);
        else Assert.Equal(3, Assert.Single(f.Sent).UserId);
    }

    [Theory]
    [InlineData("notification")]
    [InlineData("audit")]
    public async Task ReadyFailureDoesNotCommitTheTransaction(string failure)
    {
        await using var f = await SentFixtureAsync();
        await f.Service.AcceptAsync(1, 1);
        await f.Service.StartPreparingAsync(1, 1);
        f.Transaction.Invocations.Clear();
        if (failure == "notification")
            f.Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>())).ThrowsAsync(new InvalidOperationException("test"));
        else
            f.Audit.Setup(a => a.RecordRestaurantActionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(),
                It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.MarkReadyAsync(1, 1));
        f.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClosedSessionDoesNotHideOrStrandAnAlreadySentKitchenOrder()
    {
        await using var f = await SentFixtureAsync();
        (await f.Db.TableSessions.SingleAsync(s => s.Id == 1)).ClosedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        Assert.Equal(2002, (await f.Service.AcceptAsync(1, 1)).StatusId);
    }

    [Fact]
    public async Task InvalidIdsCrossRestaurantAndCancelledRequestsAreRejected()
    {
        await using var f = await SentFixtureAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.AcceptAsync(1, 0));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.AcceptAsync(0, 1));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.AcceptAsync(2, 2));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.AcceptAsync(1, 1, source.Token));
        Assert.Empty(f.Audit.Invocations);
    }

    [Fact]
    public void ExplicitFlowEndpointsHaveNoClientStatusAndDoNotBlockExistingWorkByContractExpiry()
    {
        Assert.NotNull(typeof(KitchenFlowController).GetCustomAttribute<HasPermissionAttribute>());
        foreach (var name in new[] { nameof(KitchenFlowController.Accept), nameof(KitchenFlowController.StartPreparing), nameof(KitchenFlowController.MarkReady) })
        {
            var method = typeof(KitchenFlowController).GetMethod(name)!;
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(method.GetCustomAttribute<RequireActiveRestaurantContractAttribute>());
            Assert.DoesNotContain(method.GetParameters(), parameter => parameter.GetCustomAttribute<FromBodyAttribute>() != null);
        }
    }

    /// <summary>Əvvəl göndərişi yaradır, sonra ayrıca mətbəx sorğusu kimi tracking və müşahidələri təmizləyir.</summary>
    private static async Task<Fixture> SentFixtureAsync(string? store = null, SemaphoreSlim? gate = null)
    {
        var fixture = await Fixture.CreateAsync(store, gate);
        await fixture.Service.SendAsync(1, 1, 1);
        fixture.Db.ChangeTracker.Clear();
        fixture.Sent.Clear();
        fixture.Audit.Invocations.Clear();
        fixture.Transaction.Invocations.Clear();
        fixture.LoginKitchen();
        return fixture;
    }

    /// <summary>Ümumi növbədə ikinci əməkdaşın səlahiyyətini həqiqi DB təyinatı ilə qurur.</summary>
    private static async Task MakeSecondKitchenAsync(Fixture fixture)
    {
        (await fixture.Db.UserRestaurants.SingleAsync(a => a.Id == 3)).RoleId = (int)RoleCode.Kitchen;
        await fixture.Db.SaveChangesAsync();
    }

    /// <summary>Paralel qəbul yarışında uğur və biznes konfliktini hər iki sorğunu gözləyərək müqayisə etməyə imkan verir.</summary>
    private static async Task<Exception?> CaptureAcceptAsync(Fixture fixture)
    {
        try { await fixture.Service.AcceptAsync(1, 1); return null; }
        catch (Exception error) { return error; }
    }
}
