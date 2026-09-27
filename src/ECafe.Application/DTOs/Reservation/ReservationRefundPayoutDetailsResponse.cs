namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationRefundPayoutDetailsResponse
{
    public string MaskedDetails { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
}
