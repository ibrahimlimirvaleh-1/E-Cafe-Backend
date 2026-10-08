using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;
using ECafe.Shared.Extensions;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed record ReservationArrivalWindow(DateTime MaximumDeadlineAt, DateTime? MustVacateAt);

public static class ReservationArrivalPolicy
{
    public static bool CanRecordPhysicalArrival(ReservationEntity reservation, DateTime nowUtc)
        => reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
           reservation.ArrivedAt == null &&
           reservation.ReservedAt <= nowUtc &&
           reservation.NoShowDeadlineAt > nowUtc &&
           (reservation.MustVacateAt == null || reservation.MustVacateAt > nowUtc);

    public static ErrorCode? GetSeatingConflict(ReservationEntity reservation, DateTime nowUtc)
        => reservation switch
        {
            { StatusId: var statusId } when statusId != StatusIds.Reservation(ReservationStatus.Confirmed)
                => ErrorCode.ReservationSeatingRequiresConfirmation,
            { ReservedAt: var reservedAt } when reservedAt > nowUtc
                => ErrorCode.ReservationSeatingBeforeStart,
            { ArrivedAt: null, NoShowDeadlineAt: var deadline } when deadline <= nowUtc
                => ErrorCode.ReservationSeatingWindowExpired,
            { MustVacateAt: { } mustVacateAt } when mustVacateAt <= nowUtc
                => ErrorCode.ReservationVacateTimeExpired,
            _ => null
        };

    public static bool CanRequest(ReservationEntity reservation, DateTime nowUtc)
        => reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
           reservation.ArrivedAt == null &&
           reservation.ArrivalAdjustment?.AcceptedAt == null &&
           reservation.Restaurant.IsActive && reservation.Table.IsActive && !reservation.Table.IsDeleted &&
           reservation.Table.Capacity >= reservation.PeopleCount &&
           !reservation.TableSessions.Any(s => s.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && s.ClosedAt == null) &&
           (reservation.NoShowDeadlineAt > nowUtc ||
            reservation.ArrivalAdjustment?.DecisionExpiresAt > nowUtc);

    public static ReservationArrivalWindow? GetWindow(ReservationEntity reservation, DateTime? nextReservationAt,
        ReservationTimingOptions options)
    {
        var localTime = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
            new DateTimeOffset(DateTime.SpecifyKind(reservation.ReservedAt, DateTimeKind.Utc)), reservation.Restaurant.TimeZone);
        if (!RestaurantWorkingHoursCalculator.TryGetActiveInterval(reservation.Restaurant.WorkingHours, localTime, out var interval))
            return null;

        var cap = reservation.ArrivalAdjustment?.MaximumNoShowDeadlineAt
                  ?? reservation.ReservedAt.AddMinutes(options.MaximumLateArrivalMinutes);
        cap = Min(cap, interval.EndsAt.UtcDateTime);
        var mustVacateAt = reservation.MustVacateAt;
        if (nextReservationAt.HasValue)
        {
            var nextDeadline = nextReservationAt.Value.AddMinutes(-reservation.Restaurant.TableTurnoverBufferMinutes);
            mustVacateAt = mustVacateAt.HasValue ? Min(mustVacateAt.Value, nextDeadline) : nextDeadline;
        }
        if (mustVacateAt.HasValue)
            cap = Min(cap, mustVacateAt.Value);
        return new(cap, mustVacateAt);
    }

    public static DateTime GetProposedDeadline(ReservationEntity reservation, DateTime arrivalAt, ReservationArrivalWindow window)
        => Min(arrivalAt.AddMinutes(reservation.Restaurant.NoShowGraceMinutes), window.MaximumDeadlineAt);

    public static DateTime GetDecisionDeadline(ReservationEntity reservation, DateTime nowUtc,
        ReservationArrivalWindow window, ReservationTimingOptions options)
    {
        var originalDeadline = reservation.ArrivalAdjustment?.OriginalNoShowDeadlineAt ?? reservation.NoShowDeadlineAt;
        return Min(Min(nowUtc.AddMinutes(options.ArrivalDecisionMinutes),
            originalDeadline.AddMinutes(options.ArrivalDecisionMinutes)), window.MaximumDeadlineAt);
    }

    public static IReadOnlyList<DateTimeOffset> GetChoices(ReservationEntity reservation, DateTime nowUtc,
        ReservationArrivalWindow window, ReservationTimingOptions options)
    {
        var step = TimeSpan.FromMinutes(options.ArrivalChoiceStepMinutes).Ticks;
        var start = nowUtc > reservation.ReservedAt ? nowUtc : reservation.ReservedAt;
        var next = new DateTime(((start.Ticks / step) + 1) * step, DateTimeKind.Utc);
        var choices = new List<DateTimeOffset>();
        for (; next < window.MaximumDeadlineAt; next = next.AddTicks(step))
            choices.Add(new DateTimeOffset(next));
        return choices;
    }

    // Masa təhvil və gözləmə həddindən daha erkən olan son vaxtı seçir.
    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;
}
