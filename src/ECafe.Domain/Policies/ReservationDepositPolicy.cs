using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Domain.Policies;

public static class ReservationDepositPolicy
{
    /// <summary>Provayderin təsdiqlədiyi depoziti seçir; uğursuz və yalnız başlanmış ödənişi təsdiqlənmiş hesab etmir.</summary>
    public static Payment? GetConfirmedPayment(Reservation reservation)
        => reservation.Payments.Where(payment => !payment.IsDeleted && payment.ReservationId == reservation.Id &&
                payment.RestaurantId == reservation.RestaurantId && !string.IsNullOrWhiteSpace(payment.ProviderName) &&
                !string.IsNullOrWhiteSpace(payment.ProviderRef) && payment.PaidAt.HasValue && payment.Amount > 0 &&
                (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) ||
                 payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded)))
            .OrderByDescending(payment => payment.PaidAt).ThenByDescending(payment => payment.Id).FirstOrDefault();

    /// <summary>Yeni provayder əməliyyatını və əvvəlki təsdiqlənmiş depozit tarixçəsini eyni ləğv qaydasına daxil edir.</summary>
    public static bool HasConfirmedDeposit(Reservation reservation)
        => GetConfirmedPayment(reservation) != null || reservation.PaymentProofs.Any(proof =>
            !proof.IsDeleted && proof.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed));
}
