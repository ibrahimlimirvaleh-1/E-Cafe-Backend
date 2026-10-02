using System.Globalization;
using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Enums;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.ReservationRefund.Concrete;

public static class ReservationRefundRequestMapper
{
    public static string GetCustomerNextStep(int statusId)
        => (RefundStatus)(statusId - (int)StatusType.Refund * 1000) switch
        {
            RefundStatus.Requested => "Sorğunuz göndərilib. Geri ödəniş prosesinin yenilənməsini gözləyin.",
            RefundStatus.AwaitingPayoutDetails => "Sorğunuz qəbul edilib. Köçürmə üçün geri ödəniş rekvizitlərinizi göndərin.",
            RefundStatus.ReadyForPayout => "Rekvizitləriniz göndərilib. Restoranın köçürməsini və ödəniş çekini gözləyin.",
            RefundStatus.Processing => "Restoran ödəniş çekini göndərib. Məbləğin hesabınıza çatdığını yoxlayın; yalnız bundan sonra təsdiqləyin.",
            RefundStatus.Disputed => "Etirazınız göndərilib. Restoranın köçürməni yoxlamasını və cavabını gözləyin.",
            RefundStatus.Refunded => "Geri ödəniş tamamlanıb. Əlavə sorğu göndərməyə ehtiyac yoxdur.",
            RefundStatus.Failed or RefundStatus.Rejected => "Ətraflı məlumat üçün geri ödəniş tarixçəsini yoxlayın və restoranla əlaqə saxlayın.",
            _ => "Geri ödənişin cari vəziyyətini tarixçədən izləyə bilərsiniz."
        };

    public static ReservationRefundRequestInfo? Map(ReservationEntity reservation)
    {
        if (reservation.RefundEligible != true || reservation.Refunds.Count > 0)
            return null;

        var proof = reservation.PaymentProofs
            .Where(p => p.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed))
            .OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.Id).FirstOrDefault();
        if (proof == null)
            return null;

        var amount = proof.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var message = $"{amount} AZN geri ödəniş hüququnuz var. Geri ödəniş üçün müraciət edə bilərsiniz.";
        return new(proof.Amount, "AZN", message);
    }
}
