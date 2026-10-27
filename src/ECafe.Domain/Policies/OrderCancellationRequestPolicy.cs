using System.Linq.Expressions;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Domain.Policies;

public static class OrderCancellationRequestPolicy
{
    public static Expression<Func<Order, bool>> Eligible { get; } = order =>
        !order.IsDeleted && order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing) &&
        order.SentToKitchenAt != null && order.AcceptedAt != null && order.PreparingAt != null &&
        order.ReadyAt == null && order.ServedAt == null && order.ClosedAt == null && order.CancelledAt == null &&
        order.TableSession != null && !order.TableSession.IsDeleted && order.TableSession.ClosedAt == null &&
        order.TableSession.StatusId == StatusIds.TableSession(TableSessionStatus.Open) &&
        order.TableSession.RestaurantId == order.RestaurantId && order.TableSession.TableId == order.TableId &&
        !order.TableSession.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) || payment.RequiresRefund)) &&
        !order.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) ||
             payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) || payment.RequiresRefund));

    private static readonly Func<Order, bool> EligibleCheck = Eligible.Compile();

    /// <summary>Oxunuş və yazıda eyni faktiki hazırlama, açıq sessiya və maliyyə şərtlərini yoxlayır.</summary>
    public static bool CanProcess(Order order) => EligibleCheck(order);

    /// <summary>Eyni sifariş üçün yalnız bir izlənən ləğv sorğusu yaradılır; rədd qərarı yeni sorğu ilə örtülmür.</summary>
    public static bool CanRequest(Order order) => order.CancellationRequest == null && CanProcess(order);

    /// <summary>Ofisiant yalnız hələ mətbəxə çatmamış və hazırlama mərhələsində qalan sorğunu ötürə bilər.</summary>
    public static bool CanForward(Order order) => order.CancellationRequest?.StatusId == OrderCancellationRequestStatus.AwaitingWaiter && CanProcess(order);

    /// <summary>Mətbəx qərarı yalnız özünə ötürülmüş, hələ həll edilməmiş sorğu üçün açıqdır.</summary>
    public static bool CanDecide(Order order) => order.CancellationRequest?.StatusId == OrderCancellationRequestStatus.PendingKitchen && CanProcess(order);

    /// <summary>Aktiv ödənişi və maliyyə yoxlamasını mərhələ konfliktindən ayrıca bildirir.</summary>
    public static void EnsureEligible(Order order)
    {
        if (order.TableSession?.Payments.Any(payment => !payment.IsDeleted && payment.RequiresRefund) == true)
            throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        if (order.TableSession?.Payments.Any(payment => !payment.IsDeleted && payment.PaymentStatusId ==
                StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending)) == true)
            throw new BusinessRuleException(ErrorCode.SessionBillPaymentPending);
        if (!CanProcess(order)) throw new BusinessRuleException(ErrorCode.OrderCancellationRequestNotAllowed);
    }

    /// <summary>Sorğunu müştəridən ofisianta, ofisiantdan birbaşa mətbəxə yaradır; stok və sifariş statusu dəyişmir.</summary>
    public static bool Request(Order order, bool customer, int actorId, DateTime nowUtc, string reason)
    {
        if (order.CancellationRequest != null) return false;
        EnsureEligible(order);
        order.CancellationRequest = new OrderCancellationRequest
        {
            OrderId = order.Id, Order = order, Reason = OrderCancellationPolicy.NormalizeReason(reason),
            StatusId = customer ? OrderCancellationRequestStatus.AwaitingWaiter : OrderCancellationRequestStatus.PendingKitchen,
            RequestedAt = nowUtc, RequestedByUserId = actorId,
            ForwardedAt = customer ? null : nowUtc, ForwardedByUserId = customer ? null : actorId
        };
        return true;
    }

    /// <summary>İlk ötürmə vaxtını saxlayır; ötürülmüş və ya artıq həll edilmiş qərarın təkrarı dəyişiklik etmir.</summary>
    public static bool Forward(Order order, int actorId, DateTime nowUtc)
    {
        var request = order.CancellationRequest ?? throw new BusinessRuleException(ErrorCode.OrderCancellationRequestNotFound);
        if (request.ForwardedAt.HasValue) return false;
        EnsureEligible(order);
        if (!CanForward(order)) throw new BusinessRuleException(ErrorCode.OrderCancellationRequestConflict);
        request.StatusId = OrderCancellationRequestStatus.PendingKitchen;
        request.ForwardedAt = nowUtc;
        request.ForwardedByUserId = actorId;
        return true;
    }

    /// <summary>İlk qərar və ərzaq seçimini saxlayır; təsdiq sifarişi ləğv edir, fərqli təkrar qərarı rədd edir; stok xidmətdə eyni transaction-da qaytarılır.</summary>
    public static bool Decide(Order order, bool approved, int actorId, DateTime nowUtc, string reason, bool ingredientsUnused = false)
    {
        var request = order.CancellationRequest ?? throw new BusinessRuleException(ErrorCode.OrderCancellationRequestNotFound);
        var target = approved ? OrderCancellationRequestStatus.Approved : OrderCancellationRequestStatus.Rejected;
        if (request.StatusId == target && request.ResolvedAt.HasValue)
        {
            if (approved && (request.IngredientsUnused ?? false) != ingredientsUnused)
                throw new BusinessRuleException(ErrorCode.OrderCancellationRequestConflict);
            return false;
        }
        if (request.ResolvedAt.HasValue) throw new BusinessRuleException(ErrorCode.OrderCancellationRequestConflict);
        EnsureEligible(order);
        if (!CanDecide(order)) throw new BusinessRuleException(ErrorCode.OrderCancellationRequestConflict);
        request.DecisionReason = OrderCancellationPolicy.NormalizeReason(reason);
        request.StatusId = target;
        request.ResolvedAt = nowUtc;
        request.ResolvedByUserId = actorId;
        request.IngredientsUnused = approved ? ingredientsUnused : null;
        if (approved)
        {
            order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
            order.CancelledAt = nowUtc;
            order.CancelledByUserId = actorId;
            order.CancellationReason = request.Reason;
        }
        return true;
    }

    /// <summary>Mətbəxə ötürülməmiş müştəri istəyi hazırlanma bitəndə köhnəlir; mətbəxə çatmış istək isə əvvəl qərar tələb edir.</summary>
    public static bool ExpireAwaitingWaiter(Order order, DateTime nowUtc, string reason)
    {
        if (order.CancellationRequest?.StatusId != OrderCancellationRequestStatus.AwaitingWaiter) return false;
        order.CancellationRequest.StatusId = OrderCancellationRequestStatus.Expired;
        order.CancellationRequest.ResolvedAt = nowUtc;
        order.CancellationRequest.DecisionReason = reason;
        return true;
    }
}
