using ECafe.Domain.Entities;

namespace ECafe.Domain.Services;

public readonly record struct WorkingHourInterval(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public static class RestaurantWorkingHoursCalculator
{
    public static bool TryGetActiveInterval(
        IEnumerable<RestaurantWorkingHour> workingHours,
        DateTimeOffset localDateTime,
        out WorkingHourInterval interval)
    {
        var candidates = workingHours
            .Where(hour => !hour.IsClosed)
            .SelectMany(hour => BuildCandidateIntervals(hour, localDateTime))
            .Where(candidate => candidate.StartsAt <= localDateTime && localDateTime < candidate.EndsAt)
            .OrderByDescending(candidate => candidate.EndsAt)
            .ThenByDescending(candidate => candidate.StartsAt)
            .ToList();

        if (candidates.Count == 0)
        {
            interval = default;
            return false;
        }

        interval = candidates[0];
        return true;
    }

    private static IEnumerable<WorkingHourInterval> BuildCandidateIntervals(
        RestaurantWorkingHour workingHour,
        DateTimeOffset localDateTime)
    {
        var currentDate = DateOnly.FromDateTime(localDateTime.DateTime);

        foreach (var startDate in new[] { currentDate.AddDays(-1), currentDate })
        {
            if (startDate.DayOfWeek != workingHour.DayOfWeek)
                continue;

            var start = new DateTimeOffset(startDate.ToDateTime(workingHour.OpensAt), localDateTime.Offset);
            var closeDayOffset = workingHour.CloseDayOffset;

            // Keep legacy 09:00-00:00 records compatible with overnight semantics.
            if (closeDayOffset == 0 &&
                workingHour.ClosesAt == TimeOnly.MinValue &&
                workingHour.OpensAt > workingHour.ClosesAt)
            {
                closeDayOffset = 1;
            }

            var endDate = startDate.AddDays(closeDayOffset);
            var end = new DateTimeOffset(endDate.ToDateTime(workingHour.ClosesAt), localDateTime.Offset);

            if (end > start)
                yield return new WorkingHourInterval(start, end);
        }
    }
}
