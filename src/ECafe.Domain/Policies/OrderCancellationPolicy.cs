using System.Linq.Expressions;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Domain.Policies;

public static class OrderCancellationPolicy
{
    public const int MinimumReasonLength = 3;
    public const int MaximumReasonLength = 500;

    public static Expression<Func<Order, bool>> Cancellable { get; } = order =>
        !order.IsDeleted && order.CancelledAt == null && order.ClosedAt == null &&
        order.PreparingAt == null && order.ReadyAt == null && order.ServedAt == null &&
        (order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Created) && order.AcceptedAt == null ||
         order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Accepted) &&
         order.SentToKitchenAt != null && order.AcceptedAt != null) &&
        order.TableSession != null && !order.TableSession.IsDeleted && order.TableSession.ClosedAt == null &&
        order.TableSession.StatusId == StatusIds.TableSession(TableSessionStatus.Open) &&
        order.TableSession.RestaurantId == order.RestaurantId && order.TableSession.TableId == order.TableId &&
        !order.TableSession.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) || payment.RequiresRefund)) &&
        !order.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) ||
             payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) || payment.RequiresRefund));

    private static readonly Func<Order, bool> CancellableCheck = Cancellable.Compile();

    /// <summary>Oxunuş və yazı eyni mərhələ/sessiya/ödəniş qaydasını istifadə edir; rol və stok tarixçəsi servisdə ayrıca yoxlanılır.</summary>
    public static bool CanCancel(Order order) => CancellableCheck(order);

    /// <summary>Yalnız faktiki qeydə alınmış ləğvi təkrar nəticə sayır; ilkin qərarın səbəb və vaxtını dəyişməyə imkan vermir.</summary>
    public static bool IsCancelled(Order order) => !order.IsDeleted &&
        order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled) && order.CancelledAt.HasValue;

    /// <summary>Səbəbi vahid ölçüyə salır; istifadəçi mətni üçün məcburi uzunluq sərhədlərini saxlayır.</summary>
    public static string NormalizeReason(string? reason)
    {
        var value = reason?.Trim() ?? "";
        if (value.Length is < MinimumReasonLength or > MaximumReasonLength)
            throw new BusinessRuleException(ErrorCode.OrderCancellationReasonInvalid);
        return value;
    }

    /// <summary>Hazırlanmamış sifarişin açıq sessiyasında aktiv ödəniş və ya maliyyə yoxlaması varsa ləğvi dayandırır.</summary>
    public static void EnsureCanCancel(Order order)
    {
        if (order.TableSession?.Payments.Any(payment => !payment.IsDeleted && payment.RequiresRefund) == true)
            throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        if (order.TableSession?.Payments.Any(payment => !payment.IsDeleted &&
            payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending)) == true)
            throw new BusinessRuleException(ErrorCode.SessionBillPaymentPending);
        if (!CanCancel(order)) throw new BusinessRuleException(ErrorCode.OrderCannotCancel);
    }

    /// <summary>İlk ləğvi status, vaxt, səbəb və istifadəçi ilə saxlayır; təkrar sorğu heç bir ikinci dəyişiklik etmir.</summary>
    public static bool Apply(Order order, int actorId, DateTime nowUtc, string reason)
    {
        if (IsCancelled(order)) return false;
        EnsureCanCancel(order);
        order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        order.CancelledAt = nowUtc;
        order.CancelledByUserId = actorId;
        order.CancellationReason = NormalizeReason(reason);
        return true;
    }
}
