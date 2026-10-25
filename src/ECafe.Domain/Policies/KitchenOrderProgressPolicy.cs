using Order = ECafe.Domain.Entities.Order;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class KitchenOrderProgressPolicy
{
    /// <summary>Göndərilmiş aktiv sifarişin yalnız növbəti mərhələyə keçə bildiyini müəyyən edir; UI işarəsi və server eyni qaydanı istifadə edir.</summary>
    public static bool CanApply(Order order, OrderStatus target)
        => IsActiveKitchenOrder(order) && order.StatusId == StatusIds.Build(StatusType.Order, (int)Previous(target)) &&
           (target != OrderStatus.Ready || order.CancellationRequest?.StatusId != OrderCancellationRequestStatus.PendingKitchen) &&
           GetMilestone(order, target).At == null && order.OrderItems.Any(line => !line.IsDeleted && line.Quantity > 0);

    /// <summary>İlk keçiddə vaxtı və əməkdaşı qeyd edir; eyni əməkdaşın təkrarını dəyişmədən qaytarır, başqa və ya ardıcıllığı pozan qərarı rədd edir.</summary>
    public static bool Apply(Order order, OrderStatus target, int userId, DateTime nowUtc)
    {
        if (target == OrderStatus.Ready && order.CancellationRequest?.StatusId == OrderCancellationRequestStatus.PendingKitchen)
            throw new BusinessRuleException(ErrorCode.OrderCancellationRequestPending);
        var milestone = GetMilestone(order, target);
        if (IsActiveKitchenOrder(order) && milestone.At != null && milestone.By == userId &&
            order.StatusId >= StatusIds.Build(StatusType.Order, (int)target))
            return false;
        if (!CanApply(order, target)) throw new BusinessRuleException(ErrorCode.KitchenOrderTransitionNotAllowed);

        switch (target)
        {
            case OrderStatus.Accepted: order.AcceptedAt = nowUtc; order.AcceptedByUserId = userId; break;
            case OrderStatus.Preparing: order.PreparingAt = nowUtc; order.PreparingByUserId = userId; break;
            case OrderStatus.Ready: order.ReadyAt = nowUtc; order.ReadyByUserId = userId; break;
        }
        order.StatusId = StatusIds.Build(StatusType.Order, (int)target);
        return true;
    }

    /// <summary>Bağlanmış, ləğv edilmiş, təqdim edilmiş və ya hələ göndərilməmiş sifarişdə mətbəx keçidini dayandırır.</summary>
    private static bool IsActiveKitchenOrder(Order order)
        => order.SentToKitchenAt != null && order.ClosedAt == null &&
           (order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Created) ||
            order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Accepted) ||
            order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing) ||
            order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready));

    /// <summary>Hər mətbəx əməliyyatı üçün məcburi əvvəlki statusu seçir; mərhələ atlamağa icazə vermir.</summary>
    private static OrderStatus Previous(OrderStatus target) => target switch
    {
        OrderStatus.Accepted => OrderStatus.Created,
        OrderStatus.Preparing => OrderStatus.Accepted,
        OrderStatus.Ready => OrderStatus.Preparing,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    /// <summary>Təkrar sorğunu ilk qərarın vaxtı və əməkdaşı ilə müqayisə etmək üçün saxlanmış mərhələ qeydini qaytarır.</summary>
    private static (DateTime? At, int? By) GetMilestone(Order order, OrderStatus target) => target switch
    {
        OrderStatus.Accepted => (order.AcceptedAt, order.AcceptedByUserId),
        OrderStatus.Preparing => (order.PreparingAt, order.PreparingByUserId),
        OrderStatus.Ready => (order.ReadyAt, order.ReadyByUserId),
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };
}
