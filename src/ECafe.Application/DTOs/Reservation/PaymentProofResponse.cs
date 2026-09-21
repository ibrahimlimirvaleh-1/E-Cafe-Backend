namespace ECafe.Application.DTOs.Reservation;

public sealed class PaymentProofResponse
{
    public int Id { get; init; }
    public int ReservationId { get; init; }
    public int FileId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = null!;
    public DateTime SubmittedAt { get; init; }
    public string FileViewUrl { get; init; } = null!;
}
