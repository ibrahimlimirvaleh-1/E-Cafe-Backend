using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;

namespace ECafe.Domain.Policies;

public static class ProviderPaymentPolicy
{
    /// <summary>Yalnız saxlanmış məbləğ, valyuta və əməliyyat istinadına uyğun provayder nəticəsini qəbul edir.</summary>
    public static void ValidateResult(Payment payment, string reference, decimal amount, string currency, bool isTest)
    {
        if (payment.IsDeleted || amount <= 0 || payment.ProviderRef != reference || payment.Amount != amount || payment.CurrencyCode != currency || payment.IsTest != isTest)
            throw new BusinessRuleException(ErrorCode.PaymentProviderMismatch);
    }

    /// <summary>Ödəniş nəticəsini bir dəfə tətbiq edir; gecikmiş uğurlu ödəniş rezervasiyanı diriltmir və geri qaytarılmalı kimi qeyd olunur.</summary>
    public static bool Apply(Payment payment, Reservation reservation, ProviderOperationStatus result, DateTime nowUtc, int graceMinutes)
    {
        var paid = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid);
        var refunded = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded);
        if (payment.PaymentStatusId == paid || payment.PaymentStatusId == refunded) return false;
        if (result == ProviderOperationStatus.Pending) return false;
        if ((result == ProviderOperationStatus.Failed && payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Failed)) ||
            (result == ProviderOperationStatus.Cancelled && payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Cancelled))) return false;
        if (payment.PaymentStatusId != StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending))
            throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);

        payment.ProviderStatus = result;
        payment.PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)(result switch
        {
            ProviderOperationStatus.Succeeded => PaymentStatus.Paid,
            ProviderOperationStatus.Failed => PaymentStatus.Failed,
            ProviderOperationStatus.Cancelled => PaymentStatus.Cancelled,
            _ => throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed)
        }));
        if (result != ProviderOperationStatus.Succeeded) return true;
        payment.PaidAt = nowUtc;
        reservation.PaidAt ??= nowUtc;
        var canConfirm = reservation.StatusId == StatusIds.Reservation(ReservationStatus.PendingPayment) &&
            reservation.HoldExpiresAt > nowUtc && reservation.ReservedAt > nowUtc;
        if (!canConfirm)
        {
            payment.RequiresRefund = true;
            return true;
        }
        var previous = reservation.StatusId;
        reservation.StatusId = StatusIds.Reservation(ReservationStatus.Confirmed);
        reservation.ConfirmedAt = nowUtc;
        reservation.CancellationGraceDeadlineAt = ReservationCancellationRules.GetGraceDeadline(reservation.ReservedAt, nowUtc, graceMinutes);
        reservation.HoldExpiresAt = null;
        reservation.RestaurantResponseExpiresAt = null;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previous, ToStatusId = reservation.StatusId, ChangedAt = nowUtc,
            Reason = "Provayder depozit ödənişini təsdiqlədi."
        });
        return true;
    }
}
