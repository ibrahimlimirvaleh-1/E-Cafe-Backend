namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationActionResponse
{
    public int ReservationId { get; init; }
    public int StatusId { get; init; }
    public string Status { get; init; } = null!;
    public string Message { get; init; } = null!;
}
