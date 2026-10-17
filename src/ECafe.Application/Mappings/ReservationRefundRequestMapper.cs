using System.Globalization;
using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Application.Services.Payments;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Mappings;

public static class ReservationRefundRequestMapper
{
    /// <summary>Saxlanmış ləğv və ayrılma səbəbini refund hüququnun istifadəçiyə göstərilən izahına çevirir.</summary>
    public static string GetEligibilityReason(ReservationEntity reservation)
    {
        if (reservation.DepartureReason == ReservationDepartureReason.RestaurantCannotSeat)
            return PaymentTexts.RestaurantDepartureRefundReason;
        if (reservation.DepartureReason == ReservationDepartureReason.GuestLeft)
            return PaymentTexts.GuestDepartureRefundReason;
        return reservation.CancelledByUserId == reservation.CustomerUserId
            ? "Müştəri rezervasiyanı icazə verilən ləğv müddəti daxilində ləğv edib."
            : "Restoran təsdiqlənmiş rezervasiyanı ləğv edib.";
    }
    /// <summary>Həssas rekvizit və çek tələb etməyən geri ödəniş mərhələsi izahını qaytarır.</summary>
    public static string GetCustomerNextStep(int statusId)
        => (RefundStatus)(statusId - (int)StatusType.Refund * 1000) switch
        {
            RefundStatus.Requested => "Sorğunuz göndərilib. Geri ödəniş prosesinin yenilənməsini gözləyin.",
            RefundStatus.AwaitingPayoutDetails or RefundStatus.ReadyForPayout => PaymentTexts.ManualReview,
            RefundStatus.Processing => PaymentTexts.RefundPending,
            RefundStatus.Disputed => "Etirazınız göndərilib. Restoranın köçürməni yoxlamasını və cavabını gözləyin.",
            RefundStatus.Refunded => "Geri ödəniş tamamlanıb. Əlavə sorğu göndərməyə ehtiyac yoxdur.",
            RefundStatus.Failed or RefundStatus.Rejected => "Ətraflı məlumat üçün geri ödəniş tarixçəsini yoxlayın və restoranla əlaqə saxlayın.",
            _ => "Geri ödənişin cari vəziyyətini tarixçədən izləyə bilərsiniz."
        };

    /// <summary>Saxlanmış geri ödəniş hüququnu faktiki təsdiqlənmiş depozit məbləği ilə göstərir; yeni müraciət müddəti uydurmur.</summary>
    public static ReservationRefundRequestInfo? Map(ReservationEntity reservation)
    {
        if (reservation.RefundEligible != true || reservation.Refunds.Count > 0)
            return null;

        var proof = reservation.PaymentProofs
            .Where(p => !p.IsDeleted && p.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed))
            .OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.Id).FirstOrDefault();
        var payment = ReservationDepositPolicy.GetConfirmedPayment(reservation);
        if (proof == null && payment == null)
            return null;

        var refundableAmount = payment?.Amount ?? proof!.Amount;
        var amount = refundableAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var message = $"{amount} AZN geri ödəniş hüququnuz var. Geri ödəniş üçün müraciət edə bilərsiniz.";
        return new(refundableAmount, "AZN", message);
    }
}
