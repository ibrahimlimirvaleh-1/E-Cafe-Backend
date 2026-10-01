namespace ECafe.Application.DTOs.Reservation;

public sealed record ReservationNoShowCandidate(int ReservationId, int RestaurantId, int TableId);
