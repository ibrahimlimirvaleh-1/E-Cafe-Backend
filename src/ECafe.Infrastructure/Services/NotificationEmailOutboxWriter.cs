using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Services;

public sealed class NotificationEmailOutboxWriter(ECafeDbContext context) : INotificationEmailOutboxWriter
{
    public async Task EnqueueAsync(Notification notification)
    {
        if (notification.ChannelId != (int)NotificationChannel.InApp)
            return;

        var recipient = await context.Users.AsNoTracking()
            .Where(user => user.Id == notification.UserId && user.IsActive)
            .Select(user => new { user.Email, user.Name })
            .SingleOrDefaultAsync();

        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
            return;

        var aggregateType = notification.RelatedEntityType ?? "Notification";
        var aggregateId = notification.RelatedEntityId ?? notification.UserId;
        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.NewGuid(),
            EventType = OutboxEventTypes.EmailNotificationRequested,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = JsonSerializer.Serialize(new EmailNotificationOutboxPayload
            {
                ToEmail = recipient.Email.Trim(),
                ToName = recipient.Name,
                Subject = notification.Title,
                Body = notification.Message,
                RelatedEntityType = notification.RelatedEntityType,
                RelatedEntityId = notification.RelatedEntityId
            }),
            OccurredAt = DateTime.UtcNow
        });
    }
}
