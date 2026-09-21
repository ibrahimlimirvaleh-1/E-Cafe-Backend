namespace ECafe.Domain.Services;

public static class RestaurantTimeZoneConverter
{
    public static DateTimeOffset ToRestaurantLocalTime(DateTimeOffset utcDateTime, string? timeZoneId)
    {
        var timeZone = FindTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTime(utcDateTime, timeZone);
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
