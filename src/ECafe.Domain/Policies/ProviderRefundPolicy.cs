using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class ProviderRefundPolicy
{
    /// <summary>Refundu yalnız ilkin ödənişə və tam məbləğə uyğun nəticə ilə tamamlayır; təkrar uğur tarixləri dəyişmir.</summary>
    public static bool Apply(ReservationRefund refund, Payment payment, ProviderOperationStatus result,
        string reference, decimal amount, string currency, bool isTest, DateTime nowUtc)
    {
        if (refund.SourcePaymentId != payment.Id || refund.ProviderName != payment.ProviderName ||
            refund.ProviderRef != reference || refund.Amount != amount || amount <= 0 || amount != payment.Amount || payment.PaidAt == null ||
            currency != payment.CurrencyCode || currency != refund.CurrencyCode || isTest != payment.IsTest)
            throw new BusinessRuleException(ErrorCode.PaymentProviderMismatch);
        if (refund.StatusId == StatusIds.Refund(RefundStatus.Refunded)) return false;
        if (payment.PaymentStatusId != StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid))
            throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        if (result == ProviderOperationStatus.Pending) return false;
        if (result == ProviderOperationStatus.Failed && refund.StatusId == StatusIds.Refund(RefundStatus.Failed)) return false;
        if (refund.StatusId != StatusIds.Refund(RefundStatus.Processing) || result == ProviderOperationStatus.Cancelled)
            throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        var previous = refund.StatusId;
        refund.ProviderStatus = result;
        refund.StatusId = StatusIds.Refund(result == ProviderOperationStatus.Succeeded ? RefundStatus.Refunded : RefundStatus.Failed);
        if (result == ProviderOperationStatus.Succeeded)
        {
            refund.RefundedAt = nowUtc;
            payment.RefundedAt = nowUtc;
            payment.RequiresRefund = false;
            payment.PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded);
        }
        refund.StatusHistory.Add(new ReservationRefundStatusHistory
        {
            FromStatusId = previous, ToStatusId = refund.StatusId, ChangedAt = nowUtc,
            Reason = result == ProviderOperationStatus.Succeeded ? "Provayder geri ödənişi təsdiqlədi." : "Provayder geri ödənişi tamamlamadı."
        });
        return true;
    }
}
