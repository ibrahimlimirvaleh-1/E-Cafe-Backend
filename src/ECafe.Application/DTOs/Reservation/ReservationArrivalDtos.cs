namespace ECafe.Application.DTOs.Reservation;

public sealed record ReservationArrivalOfferRequest(DateTimeOffset ArrivalAt);
public sealed record AcceptReservationArrivalRequest(Guid ConsentToken);
public sealed record ReservationArrivalOfferResponse(
    Guid ConsentToken,
    DateTimeOffset ArrivalAt,
    DateTimeOffset NoShowDeadlineAt,
    DateTimeOffset? MustVacateAt,
    DateTimeOffset DecisionExpiresAt,
    bool Accepted,
    bool HasDeposit,
    bool RefundAvailable,
    string TimeZone);
public sealed record ReservationArrivalOptionsResponse(
    bool CanRequest,
    IReadOnlyList<DateTimeOffset> ArrivalChoices,
    ReservationArrivalOfferResponse? Offer,
    string TimeZone);
