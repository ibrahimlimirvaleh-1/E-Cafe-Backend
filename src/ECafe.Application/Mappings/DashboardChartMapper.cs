using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Domain.Services;

namespace ECafe.Application.Mappings;

public static class DashboardChartMapper
{
    /// <summary>UTC vaxt hissələrini yerli gün/saat bucket-lərinə birləşdirir; yarımsaatlıq zona və DST keçidində də hadisə itirmir.</summary>
    public static DashboardChart Map(RoleCode role, DashboardPeriod period, IReadOnlyList<DashboardHour> hours)
    {
        var singleDay = period.Days == 1;
        var values = hours.GroupBy(hour => ChartBucket(hour, period, singleDay))
            .ToDictionary(group => group.Key, group => group.Sum(hour => hour.Value));
        var points = new List<DashboardPoint>();
        if (singleDay)
        {
            for (var at = period.StartUtc; at < period.EndUtc; at = at.AddHours(1))
                points.Add(new(at, values.GetValueOrDefault(at)));
        }
        else
        {
            for (var date = period.From; date <= period.To; date = date.AddDays(1))
            {
                var at = RestaurantTimeZoneConverter.GetUtcDayRange(date, period.TimeZone).StartUtc;
                points.Add(new(at, values.GetValueOrDefault(at)));
            }
        }
        return new(role == RoleCode.SuperAdmin ? "reservationCount" : role == RoleCode.Owner ? "orderAmount" : "orderCount",
            role == RoleCode.Owner ? "AZN" : "count", singleDay ? "hour" : "day", points);
    }

    /// <summary>Hadisənin UTC hissəsini seçilmiş intervalın saatına və ya faktiki yerli günün başlanğıcına aid edir.</summary>
    private static DateTime ChartBucket(DashboardHour part, DashboardPeriod period, bool singleDay)
    {
        var at = DateTime.SpecifyKind(part.UtcDate.Date.AddHours(part.UtcHour).AddMinutes(part.UtcMinute), DateTimeKind.Utc);
        if (singleDay) return period.StartUtc.AddHours(Math.Floor((at - period.StartUtc).TotalHours));
        var local = RestaurantTimeZoneConverter.ToRestaurantLocalTime(new DateTimeOffset(at), period.TimeZone);
        return RestaurantTimeZoneConverter.GetUtcDayRange(DateOnly.FromDateTime(local.DateTime), period.TimeZone).StartUtc;
    }
}
