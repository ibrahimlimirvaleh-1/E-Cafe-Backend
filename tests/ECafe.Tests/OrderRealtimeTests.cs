using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Services.Realtime;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Services.Realtime;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class OrderRealtimeTests
{
    [Theory]
    [InlineData(2002)] [InlineData(2003)] [InlineData(2004)] [InlineData(2005)]
    [InlineData(2006)] [InlineData(2007)] [InlineData(2008)]
    public async Task EveryStatusCreatesAnInvalidationWithoutCreatingAnInboxNotification(int status)
    {
        await using var db = await CreateAsync();
        var order = await db.Orders.SingleAsync();
        order.StatusId = status;
        await db.SaveChangesAsync();
        var signal = Assert.Single(await db.OutboxEvents.ToListAsync());
        Assert.Equal(OutboxEventTypes.OrderRealtimeRequested, signal.EventType);
        var payload = JsonSerializer.Deserialize<OrderRealtimePayload>(signal.Payload)!;
        Assert.Equal(1, payload.RestaurantId);
        Assert.Equal(10, payload.TableSessionId);
        Assert.Equal(100, payload.OrderId);
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.DoesNotContain("Reason", signal.Payload);
        Assert.DoesNotContain("Price", signal.Payload);
        await db.SaveChangesAsync();
        Assert.Single(await db.OutboxEvents.ToListAsync());
    }

    [Fact]
    public async Task DispatchAndCreationAreCapturedAndOrdinaryNotesDoNotGenerateStatusEvents()
    {
        await using var db = await CreateAsync();
        var order = await db.Orders.SingleAsync();
        order.Note = "Private order note";
        await db.SaveChangesAsync();
        Assert.Empty(await db.OutboxEvents.ToListAsync());
        order.SentToKitchenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.True(JsonSerializer.Deserialize<OrderRealtimePayload>((await db.OutboxEvents.SingleAsync()).Payload)!.IncludeKitchen);
        db.Orders.Add(new Order { Id = 101, RestaurantId = 1, TableId = 1, TableSessionId = 10, StatusId = 2001 });
        db.SaveChanges();
        Assert.Equal(2, await db.OutboxEvents.CountAsync());
    }

    [Theory]
    [InlineData(OrderCancellationRequestStatus.AwaitingWaiter)]
    [InlineData(OrderCancellationRequestStatus.PendingKitchen)]
    [InlineData(OrderCancellationRequestStatus.Approved)]
    [InlineData(OrderCancellationRequestStatus.Rejected)]
    [InlineData(OrderCancellationRequestStatus.Expired)]
    public async Task CancellationRequestChangesInvalidateWithoutPublishingTheFreeText(OrderCancellationRequestStatus state)
    {
        await using var db = await CreateAsync();
        var order = await db.Orders.SingleAsync();
        order.CancellationRequest = new OrderCancellationRequest { OrderId = order.Id, StatusId = state, Reason = "Secret cancellation reason" };
        await db.SaveChangesAsync();
        var signal = Assert.Single(await db.OutboxEvents.ToListAsync());
        Assert.DoesNotContain("Secret", signal.Payload);
        order.CancellationRequest.DecisionReason = "Another secret";
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.OutboxEvents.CountAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecipientsAreCurrentActiveAndRestaurantScoped(bool sentToKitchen)
    {
        await using var db = await CreateAsync();
        var recipients = await new OrderRealtimeRecipients(db).ResolveAsync(new(1, 10, 100, sentToKitchen), default);
        Assert.Equal(sentToKitchen ? new[] { 1, 2, 3 } : new[] { 2, 3 }, recipients.OrderBy(id => id));
        (await db.TableSessions.SingleAsync()).WaiterUserId = 7;
        await db.SaveChangesAsync();
        recipients = await new OrderRealtimeRecipients(db).ResolveAsync(new(1, 10, 100, sentToKitchen), default);
        Assert.Contains(7, recipients);
        Assert.DoesNotContain(2, recipients);
        Assert.DoesNotContain(4, recipients);
        Assert.DoesNotContain(5, recipients);
        Assert.DoesNotContain(6, recipients);
    }

    [Fact]
    public async Task InactiveRestaurantHasNoRecipients()
    {
        await using var db = await CreateAsync();
        (await db.Restaurants.SingleAsync(item => item.Id == 1)).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Empty(await new OrderRealtimeRecipients(db).ResolveAsync(new(1, 10, 100, true), default));
    }

    [Theory]
    [InlineData(2, 10, 100)]
    [InlineData(1, 11, 100)]
    [InlineData(1, 10, 101)]
    public async Task CorruptedOrderScopeNeverNotifiesAnotherRestaurantOrSession(int restaurant, int session, int order)
    {
        await using var db = await CreateAsync();
        Assert.Empty(await new OrderRealtimeRecipients(db).ResolveAsync(new(restaurant, session, order, true), default));
    }

    [Fact]
    public async Task NotificationReadAndCreationHaveDurableRefreshSignalsWithoutChangingInboxContent()
    {
        await using var db = await CreateAsync();
        db.Notifications.Add(new Notification { Id = 100, UserId = 2, RestaurantId = 1, Title = "Private title", Message = "Private text" });
        await db.SaveChangesAsync();
        var item = Assert.Single(await db.OutboxEvents.ToListAsync());
        Assert.Equal(OutboxEventTypes.NotificationRealtimeRequested, item.EventType);
        Assert.DoesNotContain("Private", item.Payload);
        (await db.Notifications.SingleAsync()).IsRead = true;
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.OutboxEvents.CountAsync());
    }

    [Fact]
    public async Task FailedDeliveryRemainsPendingThenRetriesWithTheSameEventIdentity()
    {
        await using var db = await CreateAsync();
        (await db.Orders.SingleAsync()).StatusId = 2004;
        await db.SaveChangesAsync();
        var item = await db.OutboxEvents.SingleAsync();
        var notifier = new Mock<IUserRealtimeNotifier>();
        notifier.SetupSequence(service => service.NotifyOrderChangedAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<OrderChangedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Private provider response"))
            .Returns(Task.CompletedTask);
        var processor = new RealtimeOutboxProcessor(db, notifier.Object, new(db));
        Assert.Equal(1, await processor.ProcessAsync(25));
        Assert.Null(item.ProcessedAt);
        Assert.Equal(1, item.RetryCount);
        Assert.Equal("RealtimeDeliveryFailed", item.LastError);
        Assert.NotNull(item.LockedUntil);
        Assert.Equal(0, await processor.ProcessAsync(25));
        item.LockedUntil = null;
        await db.SaveChangesAsync();
        Assert.Equal(1, await processor.ProcessAsync(25));
        Assert.NotNull(item.ProcessedAt);
        Assert.Null(item.LastError);
        notifier.Verify(service => service.NotifyOrderChangedAsync(It.IsAny<IReadOnlyCollection<int>>(),
            It.Is<OrderChangedEvent>(change => change.EventId == item.Id && change.RestaurantId == 1 && change.TableSessionId == 10),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(0, await processor.ProcessAsync(25));
    }

    [Fact]
    public async Task InvalidPayloadIsFinishedWithoutSendingOrLoggingPrivateData()
    {
        await using var db = await CreateAsync();
        db.OutboxEvents.Add(new OutboxEvent { Id = Guid.NewGuid(), EventType = OutboxEventTypes.OrderRealtimeRequested,
            AggregateType = "Order", Payload = "{}", OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var notifier = new Mock<IUserRealtimeNotifier>(MockBehavior.Strict);
        await new RealtimeOutboxProcessor(db, notifier.Object, new(db)).ProcessAsync(25);
        var item = await db.OutboxEvents.SingleAsync();
        Assert.NotNull(item.ProcessedAt);
        Assert.Equal("InvalidRealtimePayload", item.LastError);
    }

    [Fact]
    public async Task NotificationOutboxDoesNotSendToDeactivatedAccounts()
    {
        await using var db = await CreateAsync();
        db.Notifications.Add(new Notification { UserId = 5, Title = "Private", Message = "Private" });
        await db.SaveChangesAsync();
        var notifier = new Mock<IUserRealtimeNotifier>(MockBehavior.Strict);
        await new RealtimeOutboxProcessor(db, notifier.Object, new(db)).ProcessAsync(25);
        Assert.NotNull((await db.OutboxEvents.SingleAsync()).ProcessedAt);
    }

    /// <summary>Interceptor-suz ilkin məlumat qurur, sonra istehsaldakı interceptor ilə ayrıca context qaytarır; heç bir real baza oxunmur.</summary>
    private static async Task<ECafeDbContext> CreateAsync()
    {
        var name = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(name).Options;
        await using (var seed = new ECafeDbContext(options))
        {
            seed.Restaurants.AddRange(new Restaurant { Id = 1, Name = "One", Phone = "1", Location = "Baku", IsActive = true },
                new Restaurant { Id = 2, Name = "Two", Phone = "2", Location = "Baku", IsActive = true });
            for (var id = 1; id <= 7; id++) seed.Users.Add(new User { Id = id, Name = "Test", Surname = "User",
                Email = $"realtime{id}@example.test", Phone = id.ToString(), Password = "test", IsActive = id != 5, RoleId = 5 });
            seed.UserRestaurants.AddRange(new UserRestaurant { Id = 1, RestaurantId = 1, UserId = 1, RoleId = 6, IsActive = true },
                new UserRestaurant { Id = 2, RestaurantId = 1, UserId = 2, RoleId = 4, IsActive = true },
                new UserRestaurant { Id = 4, RestaurantId = 2, UserId = 4, RoleId = 6, IsActive = true },
                new UserRestaurant { Id = 5, RestaurantId = 1, UserId = 5, RoleId = 6, IsActive = true },
                new UserRestaurant { Id = 6, RestaurantId = 1, UserId = 6, RoleId = 3, IsActive = true },
                new UserRestaurant { Id = 7, RestaurantId = 1, UserId = 7, RoleId = 4, IsActive = true });
            seed.Tables.Add(new Table { Id = 1, RestaurantId = 1, TableNo = 1 });
            seed.TableSessions.Add(new TableSession { Id = 10, RestaurantId = 1, TableId = 1, WaiterUserId = 2, CustomerUserId = 3 });
            seed.Orders.Add(new Order { Id = 100, RestaurantId = 1, TableId = 1, TableSessionId = 10, StatusId = 2001 });
            await seed.SaveChangesAsync();
        }
        return new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(name)
            .AddInterceptors(new RealtimeOutboxInterceptor()).Options);
    }
}
