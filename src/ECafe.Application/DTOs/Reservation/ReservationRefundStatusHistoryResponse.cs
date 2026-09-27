namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationRefundStatusHistoryResponse
{
    public int Id { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = null!;
    public DateTimeOffset ChangedAt { get; init; }
    public string ActorType { get; init; } = null!;
    public string? Reason { get; init; }
}
