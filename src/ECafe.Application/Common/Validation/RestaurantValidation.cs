using ECafe.Application.DTOs.Restaurant;

namespace ECafe.Application.Common.Validation;

public static class RestaurantValidation
{
    /// <summary>Qeydiyyat və redaktədə eyni həftə gününün təkrarlanmasını rədd edir; verilməyən siyahı ayrıca qaydalara saxlanılır.</summary>
    public static bool HaveUniqueWorkingDays(IEnumerable<RestaurantWorkingHourDto>? workingHours)
    {
        if (workingHours is null)
            return true;

        var days = workingHours.Select(hour => hour.DayOfWeek).ToList();
        return days.Count == days.Distinct().Count();
    }

    /// <summary>Hər iki restoran validatorunda sistem, IANA və Windows saat qurşağı adlarını eyni qayda ilə yoxlayır.</summary>
    public static bool IsKnownTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return false;

        var trimmedTimeZone = timeZone.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(trimmedTimeZone);
            return true;
        }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }

        return TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmedTimeZone, out _)
            || TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmedTimeZone, out _);
    }
}
