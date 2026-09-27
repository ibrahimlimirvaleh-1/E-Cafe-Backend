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

    public static ReservationRefundResponse Map(ReservationRefundEntity refund)
    {
        return new ReservationRefundResponse
        {
            Id = refund.Id,
            ReservationId = refund.ReservationId,
            SourcePaymentProofId = refund.SourcePaymentProofId,
            StatusId = refund.StatusId,
            Status = refund.Status?.Name ?? RefundStatus.AwaitingPayoutDetails.GetName(),
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
            History = refund.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .ThenBy(history => history.Id)
                .Select(history => new ReservationRefundStatusHistoryResponse
                {
                    Id = history.Id,
                    FromStatus = history.FromStatus?.Name,
                    ToStatus = history.ToStatus?.Name ?? RefundStatus.AwaitingPayoutDetails.GetName(),
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
            Status = RefundStatus.Processing.GetName(),
            SubmittedAt = ToUtcOffset(transfer.SubmittedAt)
        };
    }

    public static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset? ToNullableUtcOffset(DateTime? value)
        => value.HasValue ? ToUtcOffset(value.Value) : null;
}
