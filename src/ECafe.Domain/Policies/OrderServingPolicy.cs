using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using System.Linq.Expressions;
using Order = ECafe.Domain.Entities.Order;

namespace ECafe.Domain.Policies;

public static class OrderServingPolicy
{
    /// <summary>Hazır növbənin SQL filtri ilə təqdim etmə qərarının eyni qaydanı işlətməsini təmin edir; sessiya və ofisiant icazəsi ayrıca yoxlanılır.</summary>
    public static Expression<Func<Order, bool>> ReadyToServe { get; } = order =>
        !order.IsDeleted && order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready) &&
            order.SentToKitchenAt.HasValue && order.ReadyAt.HasValue && order.ServedAt == null && order.ClosedAt == null &&
            order.OrderItems.Any(line => !line.IsDeleted && line.Quantity > 0) &&
            !order.OrderItems.Any(line => !line.IsDeleted && line.Quantity <= 0);

    private static readonly Func<Order, bool> ReadyToServeCheck = ReadyToServe.Compile();

    /// <summary>Hazır və faktiki mətbəx tarixləri olan sifarişin bir dəfə təqdim edilə bildiyini ortaq qayda ilə hesablayır.</summary>
    public static bool CanMarkServed(Order order) => ReadyToServeCheck(order);

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
