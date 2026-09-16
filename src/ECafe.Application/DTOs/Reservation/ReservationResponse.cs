namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationResponse
{
    public int Id { get; init; }
    public int RestaurantId { get; init; }
    public int TableId { get; init; }
    public DateTimeOffset ReservedAt { get; init; }
    public int PeopleCount { get; init; }
    public int StatusId { get; init; }
    public string Status { get; init; } = null!;
    public decimal DepositAmount { get; init; }
    public DateTimeOffset? HoldExpiresAt { get; init; }
    public DateTimeOffset? CancellationDeadline { get; init; }
}
