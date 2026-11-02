using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings;

public static class DashboardReportMetricMapper
{
    /// <summary>Seçilmiş hesabatın tam filtr üzrə yekunlarını göstərir; menecerin cavabına toplu maliyyə göstəricisi daxil etmir.</summary>
    public static IReadOnlyList<DashboardMetric> Map(DashboardReportKind kind, RoleCode role, DashboardReportTotals totals)
    {
        var metrics = kind switch
        {
            DashboardReportKind.Orders => new List<DashboardMetric>
            {
                Metric("totalOrders", totals.RowCount), Metric("pendingOrders", totals.PendingCount),
                Metric("cancelledOrders", totals.CancelledCount), Metric("totalItems", totals.ItemQuantity)
            },
            DashboardReportKind.Reservations => new List<DashboardMetric>
            {
                Metric("totalReservations", totals.RowCount), Metric("totalGuests", totals.GuestCount),
                Metric("arrivedReservations", totals.ArrivedCount), Metric("seatedReservations", totals.SeatedCount)
            },
            _ => new List<DashboardMetric>
            {
                Metric("collectedAmount", totals.CollectedAmount, "AZN"), Metric("refundedAmount", totals.RefundedAmount, "AZN"),
                Metric("netCollectedAmount", totals.CollectedAmount - totals.RefundedAmount, "AZN"),
                Metric("receiptCount", totals.ReceiptCount), Metric("refundCount", totals.RefundCount)
            }
        };
        if (kind == DashboardReportKind.Orders && role != RoleCode.Manager)
            metrics.Add(Metric("confirmedAmount", totals.ConfirmedAmount, "AZN"));
        return metrics;
    }

    /// <summary>Əvvəlki dövr müqayisəsi olmayan hesabat yekununu saxta artım faizi yaratmadan çevirir.</summary>
    private static DashboardMetric Metric(string key, decimal value, string unit = "count") => new(key, value, unit, null, null);
}
