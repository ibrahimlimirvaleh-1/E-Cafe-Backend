namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationRefundTransferResponse
{
    public int Id { get; init; }
    public int RefundId { get; init; }
    public decimal Amount { get; init; }
    public string? TransferReference { get; init; }
    public int ProofFileId { get; init; }
    public string ProofFileViewUrl { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset? CustomerConfirmedAt { get; init; }
    public DateTimeOffset? DisputedAt { get; init; }
    public string? DisputeReason { get; init; }
}
