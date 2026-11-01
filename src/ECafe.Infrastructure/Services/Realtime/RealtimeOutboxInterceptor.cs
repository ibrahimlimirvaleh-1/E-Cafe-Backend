using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Services.Realtime;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECafe.Infrastructure.Services.Realtime;

public sealed class RealtimeOutboxInterceptor : SaveChangesInterceptor
{
    /// <summary>Sinxron yazıda yenilənmə siqnallarını biznes dəyişiklikləri ilə eyni SaveChanges daxilinə daxil edir.</summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    /// <summary>Asinxron yazıda ayrıca transaction açmadan atomik outbox sətirlərini hazırlayır.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <summary>Yalnız aidiyyəti sifariş/sorğu və bildiriş dəyişikliklərini tutur; SaveChanges təkrarında gözləyən siqnalı çoxaltmır.</summary>
    private static void Capture(DbContext? db)
    {
        if (db is not ECafeDbContext context) return;
        context.ChangeTracker.DetectChanges();
        var pending = context.ChangeTracker.Entries<OutboxEvent>()
            .Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToList();
        var changes = context.ChangeTracker.Entries<Order>().Where(entry => entry.State is EntityState.Added or EntityState.Deleted ||
            entry.State == EntityState.Modified && new[] { nameof(Order.StatusId), nameof(Order.SentToKitchenAt),
                nameof(Order.TableSessionId), nameof(Order.WaiterUserId), nameof(Order.CustomerUserId), nameof(Order.IsDeleted),
                nameof(Order.ClosedAt) }.Any(name => entry.Property(name).IsModified)).Select(entry => entry.Entity).ToList();
        changes.AddRange(context.ChangeTracker.Entries<OrderCancellationRequest>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.Order ?? context.Orders.Local.FirstOrDefault(order => order.Id == entry.Entity.OrderId))
            .OfType<Order>());
        foreach (var order in changes.Distinct())
        {
            if (order.RestaurantId <= 0) continue;
            var id = context.Entry(order).Property(item => item.Id).IsTemporary || order.Id <= 0 ? (int?)null : order.Id;
            Add(context, pending, OutboxEventTypes.OrderRealtimeRequested, "Order", id ?? 0,
                new OrderRealtimePayload(order.RestaurantId, order.TableSessionId, id, order.SentToKitchenAt.HasValue));
        }
        foreach (var userId in context.ChangeTracker.Entries<Notification>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .Select(entry => entry.Entity.UserId).Where(id => id > 0).Distinct().ToArray())
            Add(context, pending, OutboxEventTypes.NotificationRealtimeRequested, "User", userId, new NotificationRealtimePayload(userId));
    }

    /// <summary>Eyni saxlamada eyni məzmun üçün bir siqnal yaradır; ID və vaxt çatdırılmada təkrarları tanımağa imkan verir.</summary>
    private static void Add<T>(ECafeDbContext context, List<OutboxEvent> pending, string type, string aggregate, long id, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        if (pending.Any(item => item.EventType == type && item.Payload == json)) return;
        var item = new OutboxEvent { Id = Guid.NewGuid(), EventType = type, AggregateType = aggregate,
            AggregateId = id, Payload = json, OccurredAt = DateTime.UtcNow };
        context.OutboxEvents.Add(item);
        pending.Add(item);
    }
}
