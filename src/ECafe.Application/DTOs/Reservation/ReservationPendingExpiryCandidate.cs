namespace ECafe.Application.DTOs.Reservation;

public sealed record ReservationPendingExpiryCandidate(int ReservationId, int RestaurantId, int TableId);
