using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class SessionBillMapper
{
    /// <summary>Yekun hesabı yalnız server imkanları və cari obyekt icazəsi əsasında API görünüşünə çevirir.</summary>
    public static SessionBillResponse Map(TableSession session, PaymentCapabilities capability, bool customer)
    {
        var bill = TableSessionBillingPolicy.Calculate(session);
        var latest = session.Payments.Where(payment => !payment.IsDeleted).OrderByDescending(payment => payment.Id).FirstOrDefault();
        var ready = bill.IsOpen && !bill.HasUnservedOrders && !bill.HasPendingPayment && !bill.RequiresReview;
        return new(session.Id, session.RestaurantId, session.ReservationId, TableSessionBillingPolicy.Currency,
            bill.TotalAmount, bill.DepositCreditAmount, bill.PaidAmount, bill.RemainingAmount, bill.HasUnservedOrders,
            bill.IsOpen, capability.Enabled, capability.IsTest, ready && capability.Enabled && bill.RemainingAmount > 0,
            ready && !customer && bill.RemainingAmount == 0, latest == null ? null : Map(latest));
    }

    /// <summary>Provayderin məxfi istinadını və checkout açarını göstərmədən sessiya ödənişinin nəticəsini qaytarır.</summary>
    public static SessionBillPaymentResponse Map(Payment payment) => new(payment.Id, payment.TableSessionId!.Value,
        payment.Amount, payment.CurrencyCode, payment.PaymentStatusId,
        ((PaymentStatus)(payment.PaymentStatusId % 1000)).ToString(), payment.IsTest, payment.CheckoutUrl,
        payment.PaidAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(payment.PaidAt.Value, DateTimeKind.Utc)) : null,
        payment.RequiresRefund);
}
