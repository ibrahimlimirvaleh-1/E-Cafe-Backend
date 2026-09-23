namespace ECafe.Domain.Services;

public static class RestaurantTimeZoneConverter
{
    public static DateTimeOffset ToRestaurantLocalTime(DateTimeOffset utcDateTime, string? timeZoneId)
    {
        var timeZone = FindTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTime(utcDateTime, timeZone);
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(
        DateTimeOffset utcDateTime,
        string? timeZoneId)
    {
        var timeZone = FindTimeZone(timeZoneId);
        var localDate = TimeZoneInfo.ConvertTime(utcDateTime.ToUniversalTime(), timeZone).Date;
        var localStart = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
    }

    private static TimeZoneInfo FindTimeZone(string? timeZoneId)
    {
        var trimmedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
            ? "Asia/Baku"
            : timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(trimmedTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmedTimeZoneId, out var windowsTimeZoneId))
                return TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmedTimeZoneId, out var ianaTimeZoneId))
                return TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.Utc;
    }
}
