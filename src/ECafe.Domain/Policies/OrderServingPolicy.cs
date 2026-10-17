using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Order = ECafe.Domain.Entities.Order;

namespace ECafe.Domain.Policies;

public static class OrderServingPolicy
{
    /// <summary>Hazır və faktiki mətbəx tarixləri olan sifarişin bir dəfə təqdim edilə bildiyini hesablayır; sessiya və ofisiant icazəsi xidmətdə ayrıca yoxlanılır.</summary>
    public static bool CanMarkServed(Order order)
        => !order.IsDeleted && order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready) &&
            order.SentToKitchenAt.HasValue && order.ReadyAt.HasValue && order.ServedAt == null && order.ClosedAt == null &&
            order.OrderItems.Any(line => !line.IsDeleted && line.Quantity > 0) &&
            !order.OrderItems.Any(line => !line.IsDeleted && line.Quantity <= 0);

    /// <summary>İlk təqdim vaxtını saxlayır; artıq təqdim edilmiş sifarişin təkrarında tarixi/statusu dəyişmir, digər mərhələləri rədd edir.</summary>
    public static bool Apply(Order order, DateTime nowUtc)
    {
        if (!order.IsDeleted && order.ClosedAt == null && order.ServedAt.HasValue &&
            order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Served)) return false;
        if (!CanMarkServed(order)) throw new BusinessRuleException(ErrorCode.OrderCannotMarkServed);
        order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served);
        order.ServedAt = nowUtc;
        return true;
    }
}
