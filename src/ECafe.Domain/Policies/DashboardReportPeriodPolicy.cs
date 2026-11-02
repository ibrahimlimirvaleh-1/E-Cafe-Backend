using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;

namespace ECafe.Domain.Policies;

public static class DashboardReportPeriodPolicy
{
    /// <summary>Seçilmiş yerli hesabat saatını yarımaçıq intervala çevirir; yay/qış saatı boşluğu olan sərhədi səssiz dəyişmir.</summary>
    public static DashboardPeriod Resolve(string period, DateOnly? from, DateOnly? to, string timeZone, DateTimeOffset now, int dayStartHour)
    {
        if (dayStartHour is < 0 or > 12) throw new BadRequestException(ErrorCode.DashboardFilterInvalid);
        var local = RestaurantTimeZoneConverter.ToRestaurantLocalTime(now, timeZone);
        var effectiveNow = local.Hour < dayStartHour ? now.AddDays(-1) : now;
        var value = DashboardPeriodPolicy.Resolve(period, from, to, timeZone, effectiveNow);
        if (dayStartHour == 0) return value;
        try
        {
            var start = RestaurantTimeZoneConverter.ToUtc(value.From.ToDateTime(new TimeOnly(dayStartHour, 0)), timeZone);
            var end = RestaurantTimeZoneConverter.ToUtc(value.To.AddDays(1).ToDateTime(new TimeOnly(dayStartHour, 0)), timeZone);
            var previous = RestaurantTimeZoneConverter.ToUtc(value.From.AddDays(-value.Days).ToDateTime(new TimeOnly(dayStartHour, 0)), timeZone);
            return value with { StartUtc = start, EndUtc = end, PreviousStartUtc = previous, PreviousEndUtc = start };
        }
        catch (ArgumentException) { throw new BadRequestException(ErrorCode.DashboardFilterInvalid); }
    }
}
