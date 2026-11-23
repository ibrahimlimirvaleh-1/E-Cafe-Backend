using ECafe.Application.Common.Outbox;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Outbox;

/// <summary>Növbə seçiminin SQL və InMemory yollarını saxlayır; commit və çatdırılma emalçının məsuliyyətidir.</summary>
public sealed class OutboxBatchReader(ECafeDbContext context)
{
    /// <summary>Vaxtı çatmış realtime siqnallarını izlənən batch kimi alır; paralel worker-in kilidlədiyi sətirləri gözləmir.</summary>
    public Task<List<OutboxEvent>> ReadRealtimeAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken)
        => context.Database.IsRelational()
            ? context.OutboxEvents.FromSqlInterpolated($"""
                SELECT * FROM audit.outbox_events
                WHERE "EventType" IN ({OutboxEventTypes.OrderRealtimeRequested}, {OutboxEventTypes.NotificationRealtimeRequested})
                  AND "ProcessedAt" IS NULL AND ("LockedUntil" IS NULL OR "LockedUntil" <= {nowUtc})
                ORDER BY "OccurredAt", "Id" LIMIT {batchSize} FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken)
            : context.OutboxEvents.Where(item => (item.EventType == OutboxEventTypes.OrderRealtimeRequested ||
                item.EventType == OutboxEventTypes.NotificationRealtimeRequested) && item.ProcessedAt == null &&
                (item.LockedUntil == null || item.LockedUntil <= nowUtc))
                .OrderBy(item => item.OccurredAt).Take(batchSize).ToListAsync(cancellationToken);

    /// <summary>Yalnız işlənməmiş mobil push hadisələrini seçir; realtime retry filtrini bu ayrı axına tətbiq etmir.</summary>
    public Task<List<OutboxEvent>> ReadMobilePushAsync(int batchSize, CancellationToken cancellationToken)
        => context.Database.IsRelational()
            ? context.OutboxEvents.FromSqlInterpolated($"""
                SELECT * FROM audit.outbox_events
                WHERE "EventType" = {OutboxEventTypes.MobilePushRequested}
                  AND "ProcessedAt" IS NULL
                ORDER BY "OccurredAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken)
            : context.OutboxEvents.Where(item => item.EventType == OutboxEventTypes.MobilePushRequested && item.ProcessedAt == null)
                .OrderBy(item => item.OccurredAt).Take(batchSize).ToListAsync(cancellationToken);
}
