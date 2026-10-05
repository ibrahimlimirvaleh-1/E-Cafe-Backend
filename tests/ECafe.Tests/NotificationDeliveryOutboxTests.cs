using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class NotificationDeliveryOutboxTests
{
    [Fact]
    public async Task InAppNotificationQueuesEmailAndGenericPushWithOneSave()
    {
        await using var context = CreateContext();
        context.Users.Add(NewUser());
        await context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = 1, RestaurantId = 2,
            ChannelId = (int)NotificationChannel.InApp,
            TypeId = (int)NotificationType.ReservationConfirmed,
            Title = "Reservation confirmed", Message = "Private reservation details",
            RelatedEntityType = "Reservation", RelatedEntityId = 42
        };
        context.Notifications.Add(notification);
        new MobilePushOutboxWriter(context, ReadyConfiguration()).Enqueue(notification);
        await new NotificationEmailOutboxWriter(context).EnqueueAsync(notification);

        Assert.Empty(await context.OutboxEvents.AsNoTracking().ToListAsync());
        await context.SaveChangesAsync();

        var email = Assert.Single(context.OutboxEvents.Where(x =>
            x.EventType == OutboxEventTypes.EmailNotificationRequested));
        var payload = JsonSerializer.Deserialize<EmailNotificationOutboxPayload>(email.Payload);
        Assert.NotNull(payload);
        Assert.Equal("test@example.com", payload.ToEmail);
        Assert.Equal(notification.Title, payload.Subject);
        Assert.Equal(notification.Message, payload.Body);
        Assert.Equal(42, payload.RelatedEntityId);

        var push = Assert.Single(context.OutboxEvents.Where(x =>
            x.EventType == OutboxEventTypes.MobilePushRequested));
        Assert.DoesNotContain(notification.Message, push.Payload);
    }

    [Fact]
    public async Task EmailDoesNotDependOnPushReadinessOrRestaurantScope()
    {
        await using var context = CreateContext();
        context.Users.Add(NewUser());
        await context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = 1, ChannelId = (int)NotificationChannel.InApp,
            Title = "Account alert", Message = "Check your account"
        };
        var disabledConfiguration = new ConfigurationBuilder().Build();
        new MobilePushOutboxWriter(context, disabledConfiguration).Enqueue(notification);
        await new NotificationEmailOutboxWriter(context).EnqueueAsync(notification);
        await context.SaveChangesAsync();

        Assert.Single(context.OutboxEvents);
        Assert.Equal(OutboxEventTypes.EmailNotificationRequested,
            context.OutboxEvents.Single().EventType);
    }

    [Theory]
    [InlineData(false, "test@example.com", (int)NotificationChannel.InApp)]
    [InlineData(true, "", (int)NotificationChannel.InApp)]
    [InlineData(true, "test@example.com", (int)NotificationChannel.Email)]
    public async Task EmailIsNotQueuedForIneligibleRecipientOrChannel(bool active, string email, int channel)
    {
        await using var context = CreateContext();
        var user = NewUser();
        user.IsActive = active;
        user.Email = email;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await new NotificationEmailOutboxWriter(context).EnqueueAsync(new Notification
        {
            UserId = 1, ChannelId = channel, Title = "Test", Message = "Test"
        });
        await context.SaveChangesAsync();

        Assert.Empty(context.OutboxEvents);
    }

    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser() => new()
    {
        Id = 1, Name = "Test", Surname = "User", Email = "test@example.com",
        Phone = "1", Password = "test", IsActive = true
    };

    private static IConfiguration ReadyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:PushDeliveryReady"] = "true",
            ["MobileApp:ExpoProjectId"] = "95755058-a833-443c-92c6-1b5dedf866ab"
        }).Build();
}
