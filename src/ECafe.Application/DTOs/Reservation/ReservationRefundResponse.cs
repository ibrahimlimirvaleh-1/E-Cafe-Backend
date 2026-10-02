namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationRefundResponse
{
    public int Id { get; init; }
    public int ReservationId { get; init; }
    public int? SourcePaymentProofId { get; init; }
    public int StatusId { get; init; }
    public string Status { get; init; } = null!;
    public string WorkflowFlowCode { get; init; } = null!;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? RefundedAt { get; init; }
    public string EligibilityReason { get; init; } = null!;
    public string? CustomerNextStep { get; init; }
    public string? CancellationReason { get; init; }
    public ReservationRefundPayoutDetailsResponse? PayoutDetails { get; init; }
    public ReservationRefundTransferResponse? LatestTransfer { get; init; }
    public IReadOnlyList<ReservationRefundTransferResponse> TransferAttempts { get; init; } = [];
    public IReadOnlyList<ReservationRefundStatusHistoryResponse> History { get; init; } = [];
}
