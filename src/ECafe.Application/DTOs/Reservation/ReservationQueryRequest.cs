namespace ECafe.Application.DTOs.Reservation;

public class ReservationQueryRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? StatusId { get; set; }
    public DateTimeOffset? ReservedDate { get; set; }
}

public class MyReservationsQueryRequest : ReservationQueryRequest
{
    public string? RestaurantName { get; set; }
}
