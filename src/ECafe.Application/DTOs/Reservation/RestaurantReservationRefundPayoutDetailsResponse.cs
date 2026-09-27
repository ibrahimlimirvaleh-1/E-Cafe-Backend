namespace ECafe.Application.DTOs.Reservation;

public sealed class RestaurantReservationRefundPayoutDetailsResponse
{
    public int RefundId { get; init; }
    public string Details { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
}
