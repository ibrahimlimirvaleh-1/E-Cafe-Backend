namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationRefundStatusHistoryResponse
{
    public int Id { get; init; }
    public int? FromStatusId { get; init; }
    public int ToStatusId { get; init; }
    public string? FromStatus { get; init; }
    public string? FromStatusLabel { get; init; }
    public string ToStatus { get; init; } = null!;
    public string ToStatusLabel { get; init; } = null!;
    public DateTimeOffset ChangedAt { get; init; }
    public string ActorType { get; init; } = null!;
    public string? Reason { get; init; }
    public string? ReasonLabel { get; init; }
}
