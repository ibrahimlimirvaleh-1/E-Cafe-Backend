namespace ECafe.Application.DTOs.Restaurant;

public sealed record ProposeScheduleRequest(List<RestaurantWorkingHourDto> WorkingHours, string Reason);
public sealed record ScheduleDecisionRequest(Guid Token, bool Accept, string? Note);
public sealed record ScheduleActionRequest(Guid Token);
public sealed record ScheduleParticipantResponse(int Id, int? ReservationId, int? TableSessionId,
    string TableName, string? CustomerName, DateTimeOffset ArrivalAt, DateTimeOffset? ProposedVacateAt,
    string State, bool CanAccept, string? ResponseNote);
public sealed record ScheduleChangeResponse(int Id, Guid Token, string State, string Reason,
    string TimeZone, IReadOnlyList<RestaurantWorkingHourDto> WorkingHours,
    IReadOnlyList<ScheduleParticipantResponse> Participants, bool CanApply);
public sealed record CustomerScheduleOfferResponse(int ChangeId, int ConsentId, Guid Token,
    string State, string Reason, string TimeZone, DateTimeOffset? ProposedVacateAt,
    bool CanAccept, bool CanRespond);
