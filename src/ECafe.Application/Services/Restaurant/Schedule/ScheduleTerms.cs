using System.Text.Json;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Domain.Entities;
using ECafe.Domain.Services;

namespace ECafe.Application.Services.Restaurant.Schedule;

public static class ScheduleTerms
{
    public static List<RestaurantWorkingHour> NormalizeComplete(IEnumerable<RestaurantWorkingHourDto>? hours)
    {
        var byDay = (hours ?? []).GroupBy(h => h.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        return Normalize(Enum.GetValues<DayOfWeek>().Select(day => byDay.GetValueOrDefault(day)
            ?? new RestaurantWorkingHourDto
            {
                DayOfWeek = day, OpensAt = new TimeOnly(9, 0), ClosesAt = TimeOnly.MinValue
            }));
    }

    public static void Apply(Domain.Entities.Restaurant restaurant, IEnumerable<RestaurantWorkingHour> hours)
    {
        var existing = restaurant.WorkingHours.Where(h => !h.IsDeleted).ToDictionary(h => h.DayOfWeek);
        foreach (var source in hours)
        {
            if (!existing.TryGetValue(source.DayOfWeek, out var target))
            {
                target = new RestaurantWorkingHour { RestaurantId = restaurant.Id, DayOfWeek = source.DayOfWeek };
                restaurant.WorkingHours.Add(target);
            }
            target.OpensAt = source.OpensAt;
            target.ClosesAt = source.ClosesAt;
            target.CloseDayOffset = source.CloseDayOffset;
            target.IsClosed = source.IsClosed;
        }
    }

    public static List<RestaurantWorkingHour> Normalize(IEnumerable<RestaurantWorkingHourDto> hours)
        => hours.OrderBy(h => h.DayOfWeek).Select(h => new RestaurantWorkingHour
        {
            DayOfWeek = h.DayOfWeek, OpensAt = h.OpensAt, ClosesAt = h.ClosesAt,
            CloseDayOffset = h.ClosesAt == TimeOnly.MinValue && h.OpensAt > h.ClosesAt
                ? 1 : h.CloseDayOffset ?? (h.OpensAt > h.ClosesAt ? 1 : 0),
            IsClosed = h.IsClosed
        }).ToList();

    public static string Serialize(IEnumerable<RestaurantWorkingHour> hours)
        => JsonSerializer.Serialize(hours.OrderBy(h => h.DayOfWeek).Select(h => new RestaurantWorkingHourDto
        {
            DayOfWeek = h.DayOfWeek, OpensAt = h.OpensAt, ClosesAt = h.ClosesAt,
            CloseDayOffset = h.CloseDayOffset, IsClosed = h.IsClosed
        }));

    public static List<RestaurantWorkingHour> Deserialize(string json)
        => Normalize(JsonSerializer.Deserialize<List<RestaurantWorkingHourDto>>(json)!);

    public static bool Equivalent(IEnumerable<RestaurantWorkingHour> first, IEnumerable<RestaurantWorkingHour> second)
        => Serialize(first) == Serialize(second);

    // An earlier closing affects an unbounded stay even when the arrival still fits.
    public static bool IsAffected(IEnumerable<RestaurantWorkingHour> current, IEnumerable<RestaurantWorkingHour> proposed,
        string timeZone, DateTime arrivalAt, DateTime? promisedVacateAt, out DateTime? proposedVacateAt)
    {
        proposedVacateAt = null;
        var local = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
            new DateTimeOffset(DateTime.SpecifyKind(arrivalAt, DateTimeKind.Utc)), timeZone);
        if (!RestaurantWorkingHoursCalculator.TryGetActiveInterval(proposed, local, out var next))
            return true;
        proposedVacateAt = next.EndsAt.UtcDateTime;
        if (!RestaurantWorkingHoursCalculator.TryGetActiveInterval(current, local, out var previous))
            return true;
        var promisedEnd = promisedVacateAt.HasValue && promisedVacateAt < previous.EndsAt.UtcDateTime
            ? promisedVacateAt.Value : previous.EndsAt.UtcDateTime;
        return next.EndsAt.UtcDateTime < promisedEnd;
    }
}
