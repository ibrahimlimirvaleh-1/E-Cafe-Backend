using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;
using ECafe.Shared.Extensions;

namespace ECafe.Application.Mappings;

public static class ProviderPaymentMapper
{
    /// <summary>Yalnız məbləğ, vəziyyət və yoxlanmış checkout linkini qaytarır; kart, hesab və daxili sorğu açarları çıxmır.</summary>
    public static CheckoutPaymentResponse Map(Payment payment)
        => new(payment.Id, payment.ReservationId!.Value, payment.Amount, payment.CurrencyCode, payment.PaymentStatusId,
            ((PaymentStatus)(payment.PaymentStatusId - (int)StatusType.PaymentStatus * 1000)).GetName(), payment.IsTest,
            payment.CheckoutUrl, Utc(payment.CheckoutExpiresAt), Utc(payment.PaidAt), Utc(payment.RefundedAt), payment.RequiresRefund);

    /// <summary>Vaxtı UTC offset ilə göstərir; baş verməmiş mərhələdə null saxlayır.</summary>
    private static DateTimeOffset? Utc(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
