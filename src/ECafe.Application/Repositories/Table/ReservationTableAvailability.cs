namespace ECafe.Application.Repositories.Table;

public sealed record ReservationTableAvailability(
    Domain.Entities.Table Table,
    DateTime? MustVacateAt);
