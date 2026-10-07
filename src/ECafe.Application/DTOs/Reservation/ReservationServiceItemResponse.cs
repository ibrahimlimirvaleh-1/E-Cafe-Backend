namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationServiceItemResponse
{
    public int Id { get; init; }
    public int RestaurantId { get; init; }
    public int TableId { get; init; }
    public string TableName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int PeopleCount { get; init; }
    public int StatusId { get; init; }
    public string? TimeZone { get; init; }
    public DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset NoShowDeadlineAt { get; init; }
    public DateTimeOffset? MustVacateAt { get; init; }
    public DateTimeOffset? ArrivedAt { get; init; }
    public DateTimeOffset? SeatedAt { get; init; }
}
