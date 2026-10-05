using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Services;

public sealed class MobilePushOutboxWriter(ECafeDbContext context, IConfiguration configuration)
    : IMobilePushOutboxWriter
{
    public void Enqueue(Notification notification)
    {
        if (!configuration.GetValue<bool>("MobileApp:PushDeliveryReady") ||
            !Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out _) ||
            notification.RestaurantId is not > 0 ||
            notification.ChannelId != (int)NotificationChannel.InApp)
            return;

        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.NewGuid(),
            EventType = OutboxEventTypes.MobilePushRequested,
            AggregateType = notification.RelatedEntityType ?? "Notification",
            AggregateId = notification.RelatedEntityId ?? 0,
            Payload = JsonSerializer.Serialize(new MobilePushOutboxPayload(
                notification.UserId, notification.RestaurantId.Value)),
            OccurredAt = DateTime.UtcNow
        });
    }
}
