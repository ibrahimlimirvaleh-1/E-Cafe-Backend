using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Services.Realtime;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Services.Realtime;

public sealed class RealtimeOutboxProcessor(ECafeDbContext context, IUserRealtimeNotifier notifier, OrderRealtimeRecipients recipients)
{
    /// <summary>Yalnız commit olunmuş siqnalları SKIP LOCKED ilə seçir; çatdırılmayan sətirləri məxfi xəta yazmadan təkrar üçün saxlayır.</summary>
    public async Task<int> ProcessAsync(int batchSize, CancellationToken token = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 100);
        var now = DateTime.UtcNow;
        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(token) : null;
        var events = context.Database.IsRelational()
            ? await context.OutboxEvents.FromSqlInterpolated($"""
                SELECT * FROM audit.outbox_events
                WHERE "EventType" IN ({OutboxEventTypes.OrderRealtimeRequested}, {OutboxEventTypes.NotificationRealtimeRequested})
                  AND "ProcessedAt" IS NULL AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                ORDER BY "OccurredAt", "Id" LIMIT {batchSize} FOR UPDATE SKIP LOCKED
                """).ToListAsync(token)
            : await context.OutboxEvents.Where(item => (item.EventType == OutboxEventTypes.OrderRealtimeRequested ||
                item.EventType == OutboxEventTypes.NotificationRealtimeRequested) && item.ProcessedAt == null &&
                (item.LockedUntil == null || item.LockedUntil <= now)).OrderBy(item => item.OccurredAt).Take(batchSize).ToListAsync(token);
        foreach (var item in events)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await DeliverAsync(item, timeout.Token);
                item.ProcessedAt = DateTime.UtcNow;
                item.LockedUntil = null;
                item.LastError = null;
            }
            catch (JsonException)
            {
                item.ProcessedAt = DateTime.UtcNow;
                item.LastError = "InvalidRealtimePayload";
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                item.RetryCount = Math.Min(item.RetryCount + 1, 1000);
                item.LockedUntil = DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(item.RetryCount, 9))));
                item.LastError = "RealtimeDeliveryFailed";
            }
        }
        await context.SaveChangesAsync(token);
        if (transaction != null) await transaction.CommitAsync(token);
        return events.Count;
    }

    /// <summary>Yalnız etibarlı scope və aktiv auditoriyaya identifikator siqnalı göndərir; bildirişin özü və qiymətlər göndərilmir.</summary>
    private async Task DeliverAsync(OutboxEvent item, CancellationToken token)
    {
        if (item.EventType == OutboxEventTypes.OrderRealtimeRequested)
        {
            var payload = JsonSerializer.Deserialize<OrderRealtimePayload>(item.Payload);
            if (payload == null || payload.RestaurantId <= 0 || payload.TableSessionId is <= 0 || payload.OrderId is <= 0)
                throw new JsonException();
            var users = await recipients.ResolveAsync(payload, token);
            if (users.Length > 0) await notifier.NotifyOrderChangedAsync(users,
                new OrderChangedEvent(item.Id, payload.RestaurantId, payload.TableSessionId, payload.OrderId, item.OccurredAt), token);
        }
        else
        {
            var payload = JsonSerializer.Deserialize<NotificationRealtimePayload>(item.Payload);
            if (payload == null || payload.UserId <= 0) throw new JsonException();
            if (await context.Users.AnyAsync(user => user.Id == payload.UserId && user.IsActive, token))
                await notifier.NotifyNotificationsChangedAsync(payload.UserId, token);
        }
    }
}
