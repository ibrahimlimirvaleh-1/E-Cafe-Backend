using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using ECafe.Shared.Extensions;
using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ReservationRefundTransferEntity = ECafe.Domain.Entities.ReservationRefundTransfer;

namespace ECafe.Application.Services.ReservationRefund.Concrete;

internal static class ReservationRefundResponseMapper
{
    private static string RefundFlowCode => WorkflowFlowCode.FromStatusType(StatusType.Refund);

    public static ReservationRefundResponse Map(
        ReservationRefundEntity refund,
        IFileAccessUrlService fileAccessUrlService)
    {
        var transfers = refund.TransferAttempts
            .OrderByDescending(transfer => transfer.SubmittedAt)
            .ThenByDescending(transfer => transfer.Id)
            .Select(transfer => MapTransfer(refund, transfer, fileAccessUrlService))
            .ToList();

        return new ReservationRefundResponse
        {
            Id = refund.Id,
            ReservationId = refund.ReservationId,
            SourcePaymentProofId = refund.SourcePaymentProofId,
            StatusId = refund.StatusId,
            Status = ResolveStatusName(refund.StatusId, refund.Status?.Name),
            WorkflowFlowCode = RefundFlowCode,
            Amount = refund.Amount,
            CurrencyCode = refund.CurrencyCode,
            RequestedAt = ToUtcOffset(refund.RequestedAt),
            ApprovedAt = ToNullableUtcOffset(refund.ApprovedAt),
            RefundedAt = ToNullableUtcOffset(refund.RefundedAt),
            EligibilityReason = refund.EligibilityReason,
            CancellationReason = refund.CancellationReasonSnapshot,
            PayoutDetails = refund.PayoutDetails is null
                ? null
                : new ReservationRefundPayoutDetailsResponse
                {
                    MaskedDetails = refund.PayoutDetails.MaskedDetails,
                    SubmittedAt = ToUtcOffset(refund.PayoutDetails.SubmittedAt)
                },
            LatestTransfer = transfers.FirstOrDefault(),
            TransferAttempts = transfers,
            History = refund.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .ThenBy(history => history.Id)
                .Select(history => new ReservationRefundStatusHistoryResponse
                {
                    Id = history.Id,
                    FromStatus = history.FromStatus?.Name,
                    ToStatus = ResolveStatusName(history.ToStatusId, history.ToStatus?.Name),
                    ChangedAt = ToUtcOffset(history.ChangedAt),
                    ActorType = history.ChangedByUserId is null
                        ? "System"
                        : history.ChangedByUserId == refund.Reservation.CustomerUserId
                            ? "Customer"
                            : "Restaurant",
                    Reason = history.Reason
                })
                .ToList()
        };
    }

    public static ReservationRefundTransferResponse MapTransfer(
        ReservationRefundEntity refund,
        ReservationRefundTransferEntity transfer,
        IFileAccessUrlService fileAccessUrlService)
    {
        var proofFileId = transfer.ProofFileId
            ?? throw new InvalidOperationException("A refund transfer requires a proof file.");

        return new ReservationRefundTransferResponse
        {
            Id = transfer.Id,
            RefundId = refund.Id,
            Amount = transfer.Amount,
            TransferReference = transfer.TransferReference,
            ProofFileId = proofFileId,
            ProofFileViewUrl = fileAccessUrlService.BuildViewUrl(proofFileId),
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

    private static string ResolveStatusName(int statusId, string? loadedName)
    {
        var statusValue = statusId - (int)StatusType.Refund * 1000;
        return Enum.IsDefined(typeof(RefundStatus), statusValue)
            ? ((RefundStatus)statusValue).GetName()
            : loadedName ?? statusId.ToString();
    }

    public static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset? ToNullableUtcOffset(DateTime? value)
        => value.HasValue ? ToUtcOffset(value.Value) : null;
}
