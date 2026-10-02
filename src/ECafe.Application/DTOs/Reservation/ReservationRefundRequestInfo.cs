namespace ECafe.Application.DTOs.Reservation;

public sealed record ReservationRefundRequestInfo(
    decimal Amount,
    string CurrencyCode,
    string Message);
