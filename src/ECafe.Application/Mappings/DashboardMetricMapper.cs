using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class DashboardMetricMapper
{
    /// <summary>Rolun məqsədinə uyğun dörd göstərici yaradır; canlı saylara tarixi dəyişmə faizi uydurmur.</summary>
    public static IReadOnlyList<DashboardMetric> Map(RoleCode role, DashboardTotals current, DashboardTotals previous, DashboardLive live)
        => role switch
        {
            RoleCode.SuperAdmin => [Metric("activeRestaurants", live.ActiveRestaurants), Metric("activeUsers", live.ActiveUsers),
                Metric("reservationCount", current.ReservationCount, previous.ReservationCount), Metric("activeContracts", live.ActiveContracts)],
            RoleCode.Owner => [Metric("orderAmount", current.OrderAmount, previous.OrderAmount, "AZN"),
                Metric("collectedAmount", current.CollectedAmount, previous.CollectedAmount, "AZN"),
                Metric("orderCount", current.OrderCount, previous.OrderCount),
                Metric("averageOrder", Average(current), Average(previous), "AZN")],
            _ => [Metric("todayReservations", live.TodayReservations), Metric("openSessions", live.OpenSessions),
                Metric("kitchenOrders", live.KitchenOrders), Metric("readyOrders", live.ReadyOrders)]
        };

    /// <summary>Əvvəlki müqayisə yoxdursa null saxlayır və sıfır bazaya bölünmənin qarşısını alır.</summary>
    private static DashboardMetric Metric(string key, decimal value, decimal? previous = null, string unit = "count")
        => new(key, value, unit, previous, previous.HasValue ? DashboardPeriodPolicy.ChangePercent(value, previous.Value) : null);

    /// <summary>Sifarişlərin saxlanmış məbləğinin orta qiymətini hesablayır; sifariş yoxdursa sıfır qaytarır.</summary>
    private static decimal Average(DashboardTotals totals)
        => totals.OrderCount == 0 ? 0 : decimal.Round(totals.OrderAmount / totals.OrderCount, 2);
}
