using ECafe.Domain.Exceptions;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Services;

namespace ECafe.Domain.Policies;

public sealed record DashboardPeriod(DateOnly From, DateOnly To, string TimeZone, DateTime StartUtc, DateTime EndUtc,
    DateTime PreviousStartUtc, DateTime PreviousEndUtc)
{
    public int Days => To.DayNumber - From.DayNumber + 1;
}

public static class DashboardPeriodPolicy
{
    public const string DefaultTimeZone = "Asia/Baku";
    public const int MaximumDays = 92;
    public const int PageSize = 10;
    public const int MaximumPageIndex = 100000;
    public const int RestaurantOptionLimit = 200;
    public const int ContractWarningDays = 14;
    public const int KitchenDelayMinutes = 25;
    public const int ChartBucketMinutes = 15;

    /// <summary>Tarix filtrini hesabat saat qurşağında yarımaçıq UTC intervalına və eyni uzunluqlu əvvəlki dövrə çevirir.</summary>
    public static DashboardPeriod Resolve(string period, DateOnly? from, DateOnly? to, string timeZone, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(RestaurantTimeZoneConverter.ToRestaurantLocalTime(now, timeZone).DateTime);
        var first = period switch
        {
            "today" => today,
            "week" => today.AddDays(-6),
            "month" => today.AddDays(-29),
            "custom" when from.HasValue && to.HasValue => from.Value,
            _ => throw new BadRequestException(ErrorCode.DashboardFilterInvalid)
        };
        var last = period == "custom" ? to!.Value : today;
        var days = last.DayNumber - first.DayNumber + 1;
        if (days is < 1 or > MaximumDays || first.Year < 2000 || last.Year > 2100)
            throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        var start = RestaurantTimeZoneConverter.GetUtcDayRange(first, timeZone).StartUtc;
        var end = RestaurantTimeZoneConverter.GetUtcDayRange(last, timeZone).EndUtc;
        var previousStart = RestaurantTimeZoneConverter.GetUtcDayRange(first.AddDays(-days), timeZone).StartUtc;
        return new(first, last, timeZone, start, end, previousStart, start);
    }

    /// <summary>Əvvəlki dövr sıfır olduqda saxta artım faizi yaratmır; digər hallarda iki rəqəm dəqiqliyi ilə hesablayır.</summary>
    public static decimal? ChangePercent(decimal current, decimal previous)
        => previous == 0 ? null : decimal.Round((current - previous) / Math.Abs(previous) * 100, 2);
}
