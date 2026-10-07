namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationHistoryResponse
{
    public int ReservationId { get; init; }

    public DateTimeOffset? ArrivedAt { get; init; }

    public DateTimeOffset? SeatedAt { get; init; }

    public IReadOnlyList<ReservationHistoryItemResponse> Items { get; init; }
        = Array.Empty<ReservationHistoryItemResponse>();
}

public sealed class ReservationHistoryItemResponse
{
    public int Id { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = null!;
    public DateTimeOffset ChangedAt { get; init; }
    public string ActorType { get; init; } = null!;
    public string? Reason { get; init; }
}
