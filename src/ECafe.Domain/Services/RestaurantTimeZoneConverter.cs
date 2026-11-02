using ECafe.Domain.Constants;

namespace ECafe.Domain.Services;

public static class RestaurantTimeZoneConverter
{
    /// <summary>Restoranın yerli tarix-saatını həmin tarixdəki saat qurşağı qaydaları ilə UTC-yə çevirir.</summary>
    public static DateTime ToUtc(DateTime localDateTime, string? timeZoneId)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), FindTimeZone(timeZoneId));

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
        return GetUtcDayRange(DateOnly.FromDateTime(localDate), timeZoneId);
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(
        DateOnly localDate,
        string? timeZoneId)
    {
        var timeZone = FindTimeZone(timeZoneId);
        var localStart = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
    }

    /// <summary>Standart restoran zonasını və platformalararası IANA/Windows adlarını həll edir; mövcud UTC ehtiyat davranışını saxlayır.</summary>
    private static TimeZoneInfo FindTimeZone(string? timeZoneId)
    {
        var trimmedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
            ? RestaurantDefaults.TimeZone
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
