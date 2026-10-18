using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Payments;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Domain.Workflow;
using ECafe.Shared.Extensions;
using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ReservationRefundTransferEntity = ECafe.Domain.Entities.ReservationRefundTransfer;

namespace ECafe.Application.Mappings;

public static class ReservationRefundResponseMapper
{
    private static string RefundFlowCode => WorkflowFlowCode.FromStatusType(StatusType.Refund);

    /// <summary>Geri ödənişin maliyyə tarixçəsini verir; köhnə rekvizitləri və çek fayllarını artıq API-yə çıxarmır.</summary>
    public static ReservationRefundResponse Map(
        ReservationRefundEntity refund,
        IFileAccessUrlService fileAccessUrlService)
    {
        return new ReservationRefundResponse
        {
            Id = refund.Id,
            ReservationId = refund.ReservationId,
            SourcePaymentProofId = refund.SourcePaymentProofId,
            SourcePaymentId = refund.SourcePaymentId,
            IsTest = refund.SourcePayment?.IsTest == true,
            RequiresManualReview = refund.SourcePaymentId == null && refund.StatusId != StatusIds.Refund(RefundStatus.Refunded),
            CanSimulate = refund.SourcePayment?.IsTest == true && refund.StatusId == StatusIds.Refund(RefundStatus.Processing),
            StatusId = refund.StatusId,
            Status = ResolveStatusName(refund.StatusId, refund.Status?.Name),
            StatusLabel = ResolveStatusName(refund.StatusId, refund.Status?.Name),
            WorkflowFlowCode = RefundFlowCode,
            Amount = refund.Amount,
            CurrencyCode = refund.CurrencyCode,
            RequestedAt = ToUtcOffset(refund.RequestedAt),
            ApprovedAt = ToNullableUtcOffset(refund.ApprovedAt),
            RefundedAt = ToNullableUtcOffset(refund.RefundedAt),
            EligibilityReason = refund.EligibilityReason,
            CustomerNextStep = refund.SourcePaymentId == null && refund.StatusId != StatusIds.Refund(RefundStatus.Refunded)
                ? PaymentTexts.ManualReview : ReservationRefundRequestMapper.GetCustomerNextStep(refund.StatusId),
            CancellationReason = refund.CancellationReasonSnapshot,
            PayoutDetails = null,
            LatestTransfer = null,
            TransferAttempts = [],
            History = refund.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .ThenBy(history => history.Id)
                .Select(history => new ReservationRefundStatusHistoryResponse
                {
                    Id = history.Id,
                    FromStatusId = history.FromStatusId,
                    ToStatusId = history.ToStatusId,
                    FromStatus = history.FromStatus?.Name,
                    FromStatusLabel = history.FromStatusId.HasValue
                        ? ResolveStatusName(history.FromStatusId.Value, history.FromStatus?.Name) : null,
                    ToStatus = ResolveStatusName(history.ToStatusId, history.ToStatus?.Name),
                    ToStatusLabel = ResolveStatusName(history.ToStatusId, history.ToStatus?.Name),
                    ChangedAt = ToUtcOffset(history.ChangedAt),
                    ActorType = history.ChangedByUserId is null
                        ? "System"
                        : history.ChangedByUserId == refund.Reservation.CustomerUserId
                            ? "Customer"
                            : "Restaurant",
                    Reason = history.Reason,
                    ReasonLabel = ResolveGeneratedHistoryReason(history)
                })
                .ToList()
        };
    }

    /// <summary>Əvvəlki köçürmənin yalnız məbləğ və statusunu verir; istifadəçinin yazdığı istinad və çek URL-si gizlidir.</summary>
    public static ReservationRefundTransferResponse MapTransfer(
        ReservationRefundEntity refund,
        ReservationRefundTransferEntity transfer,
        IFileAccessUrlService fileAccessUrlService)
    {
        return new ReservationRefundTransferResponse
        {
            Id = transfer.Id,
            RefundId = refund.Id,
            Amount = transfer.Amount,
            TransferReference = null,
            ProofFileId = 0,
            ProofFileViewUrl = string.Empty,
            Status = transfer.CustomerConfirmedAt.HasValue
                ? RefundStatus.Refunded.GetName()
                : transfer.DisputedAt.HasValue
                    ? RefundStatus.Disputed.GetName()
                    : RefundStatus.Processing.GetName(),
            SubmittedAt = ToUtcOffset(transfer.SubmittedAt),
            CustomerConfirmedAt = ToNullableUtcOffset(transfer.CustomerConfirmedAt),
            DisputedAt = ToNullableUtcOffset(transfer.DisputedAt),
            DisputeReason = transfer.DisputeReason
        };
    }

    /// <summary>Yalnız məlum provayder mərhələlərinin sistem səbəbini görünüş üçün ayırır; sərbəst qeydləri və saxlanmış tarixçəni dəyişmir.</summary>
    private static string? ResolveGeneratedHistoryReason(Domain.Entities.ReservationRefundStatusHistory history)
    {
        var status = (RefundStatus)(history.ToStatusId - (int)StatusType.Refund * 1000);
        return (status, history.Reason) switch
        {
            (RefundStatus.Processing, PaymentTexts.RefundPending) => history.Reason,
            (RefundStatus.AwaitingPayoutDetails, PaymentTexts.ManualReview) => history.Reason,
            (RefundStatus.Failed, ProviderRefundPolicy.FailedReason) when history.ChangedByUserId is null => history.Reason,
            (RefundStatus.Refunded, ProviderRefundPolicy.SucceededReason) when history.ChangedByUserId is null => history.Reason,
            _ => null
        };
    }

    /// <summary>Status adını mərkəzi enumdan həll edir; köhnə bazada ad yüklənməsə ID-ni saxlayır.</summary>
    private static string ResolveStatusName(int statusId, string? loadedName)
    {
        var statusValue = statusId - (int)StatusType.Refund * 1000;
        return Enum.IsDefined(typeof(RefundStatus), statusValue)
            ? ((RefundStatus)statusValue).GetName()
            : loadedName ?? statusId.ToString();
    }

    /// <summary>Saxlanmış vaxtı UTC offset ilə cavaba çevirir.</summary>
    public static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    /// <summary>Hələ baş verməmiş tarix üçün null saxlayır.</summary>
    private static DateTimeOffset? ToNullableUtcOffset(DateTime? value)
        => value.HasValue ? ToUtcOffset(value.Value) : null;
}
