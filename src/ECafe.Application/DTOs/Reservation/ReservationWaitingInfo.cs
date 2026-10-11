namespace ECafe.Application.DTOs.Reservation;

public sealed record ReservationWaitingInfo(
    DateTimeOffset WaitingSinceAt, int WaitingMinutes, int ServiceDelayMinutes, bool IsOverdue);
