using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Domain.Policies;

public sealed record TableSessionBill(decimal TotalAmount, decimal DepositCreditAmount, decimal PaidAmount,
    decimal RemainingAmount, bool HasUnservedOrders, bool HasPendingPayment, bool RequiresReview, bool IsOpen, string Fingerprint);

public static class TableSessionBillingPolicy
{
    public const string Currency = "AZN";
    private const decimal MaximumAmount = 99999999.99m;

    /// <summary>Saxlanmış sifariş qiymətlərini toplayır; ləğv edilmiş sətirləri, təsdiqlənməmiş və qaytarılmış ödənişləri borcdan çıxmır.</summary>
    public static TableSessionBill Calculate(TableSession session)
    {
        var orders = session.Orders.Where(order => !order.IsDeleted &&
            order.StatusId != OrderStatusId(OrderStatus.Cancelled)).OrderBy(order => order.Id).ToList();
        decimal total = 0;
        var signature = new StringBuilder().Append(session.Id).Append('|');
        foreach (var order in orders)
        {
            if (order.RestaurantId != session.RestaurantId || order.TableSessionId != session.Id || order.TableId != session.TableId)
                throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
            var lines = order.OrderItems.Where(line => !line.IsDeleted).OrderBy(line => line.Id).ToList();
            if (lines.Count == 0) throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
            foreach (var line in lines)
            {
                if (line.Quantity <= 0 || line.UnitPriceSnapshot < 0 || line.LineTotal < 0 ||
                    line.LineTotal != decimal.Round(line.Quantity * line.UnitPriceSnapshot, 2) ||
                    line.LineTotal != decimal.Round(line.LineTotal, 2))
                    throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
                total += line.LineTotal;
                signature.Append(order.Id).Append(':').Append(line.Id).Append(':').Append(line.Quantity)
                    .Append(':').Append(line.LineTotal.ToString("G29", CultureInfo.InvariantCulture)).Append('|');
            }
        }
        if (total > MaximumAmount) throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        var deposit = GetDepositCredit(session, total);
        signature.Append("deposit:").Append(deposit.ToString("G29", CultureInfo.InvariantCulture));
        var payments = session.Payments.Where(payment => !payment.IsDeleted &&
            payment.TableSessionId == session.Id && payment.RestaurantId == session.RestaurantId &&
            payment.ReservationId == null && payment.OrderId == null).ToList();
        var paid = payments.Where(payment => payment.PaymentStatusId == PaymentStatusId(PaymentStatus.Paid) &&
            payment.PaidAt.HasValue && payment.RefundedAt == null && !payment.RequiresRefund && payment.Amount > 0 &&
            payment.CurrencyCode == Currency && !string.IsNullOrWhiteSpace(payment.ProviderName) &&
            !string.IsNullOrWhiteSpace(payment.ProviderRef)).Sum(payment => payment.Amount);
        var pending = payments.Any(payment => payment.PaymentStatusId == PaymentStatusId(PaymentStatus.Pending));
        var review = payments.Any(payment => payment.RequiresRefund);
        var unserved = orders.Any(order => order.StatusId != OrderStatusId(OrderStatus.Served) &&
            order.StatusId != OrderStatusId(OrderStatus.Closed) || order.ServedAt == null);
        return new(total, deposit, paid, Math.Max(0, total - deposit - paid), unserved, pending, review,
            !session.IsDeleted && session.ClosedAt == null && session.StatusId == StatusIds.TableSession(TableSessionStatus.Open),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString()))));
    }

    /// <summary>Həm menecer, həm ofisiant bağlanışında xidmətin tamamlandığını və hesabın borcsuz olduğunu məcburi yoxlayır.</summary>
    public static void EnsureCanClose(TableSession session)
    {
        var bill = Calculate(session);
        if (bill.RequiresReview) throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        if (bill.HasUnservedOrders) throw new BusinessRuleException(ErrorCode.SessionBillOrdersUnserved);
        if (bill.HasPendingPayment) throw new BusinessRuleException(ErrorCode.SessionBillPaymentPending);
        if (bill.RemainingAmount > 0) throw new BusinessRuleException(ErrorCode.SessionBillUnpaid);
    }

    /// <summary>Ödənişin təsdiqini bir dəfə tətbiq edir; dəyişmiş hesaba aid uğuru borcdan çıxmaq əvəzinə yoxlama üçün işarələyir.</summary>
    public static bool ApplyPayment(TableSession session, Payment payment, ProviderOperationStatus outcome, DateTime nowUtc)
    {
        if (!Enum.IsDefined(outcome)) throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        if (payment.PaymentStatusId != PaymentStatusId(PaymentStatus.Pending)) return false;
        if (outcome == ProviderOperationStatus.Pending) return false;
        var bill = Calculate(session);
        payment.ProviderStatus = outcome;
        payment.PaymentStatusId = PaymentStatusId(outcome switch
        {
            ProviderOperationStatus.Succeeded => PaymentStatus.Paid,
            ProviderOperationStatus.Failed => PaymentStatus.Failed,
            ProviderOperationStatus.Cancelled => PaymentStatus.Cancelled,
            _ => throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed)
        });
        if (outcome == ProviderOperationStatus.Succeeded)
        {
            payment.PaidAt = nowUtc;
            payment.RequiresRefund = !bill.IsOpen || bill.HasUnservedOrders || bill.Fingerprint != payment.BillFingerprint ||
                bill.RemainingAmount != payment.Amount;
        }
        return true;
    }

    /// <summary>Depoziti yalnız ilk sessiyada istifadə edir; yeni provayder ödənişini və əvvəllər təsdiqlənmiş depoziti iki dəfə saymır, refundlu məbləği çıxmır.</summary>
    private static decimal GetDepositCredit(TableSession session, decimal total)
    {
        var reservation = session.Reservation;
        if (reservation?.DepositAmount < 0) throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        if (reservation == null || reservation.IsDeleted || reservation.Id != session.ReservationId ||
            reservation.RestaurantId != session.RestaurantId || reservation.TableSessions.Any(other =>
                !other.IsDeleted && other.Id != session.Id && other.Id < session.Id) || reservation.Refunds.Any(refund =>
                !refund.IsDeleted && refund.StatusId != StatusIds.Refund(RefundStatus.Rejected))) return 0;
        var payment = ReservationDepositPolicy.GetConfirmedPayment(reservation);
        if (payment == null)
        {
            var legacy = reservation.PaymentProofs.Where(proof => !proof.IsDeleted && proof.ReservationId == reservation.Id &&
                    proof.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) && proof.Amount > 0)
                .OrderByDescending(proof => proof.ReviewedAt).ThenByDescending(proof => proof.Id).FirstOrDefault();
            return legacy == null ? 0 : Math.Min(total, Math.Min(reservation.DepositAmount, legacy.Amount));
        }
        if (payment.RequiresRefund || payment.RefundedAt.HasValue || payment.CurrencyCode != Currency ||
            payment.PaymentStatusId != PaymentStatusId(PaymentStatus.Paid)) return 0;
        return Math.Min(total, Math.Min(reservation.DepositAmount, payment.Amount));
    }

    /// <summary>Sifariş mərhələsini ümumi status identifikatoruna çevirir.</summary>
    private static int OrderStatusId(OrderStatus status) => StatusIds.Build(StatusType.Order, (int)status);
    /// <summary>Ödəniş mərhələsini ümumi status identifikatoruna çevirir.</summary>
    private static int PaymentStatusId(PaymentStatus status) => StatusIds.Build(StatusType.PaymentStatus, (int)status);
}
