namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardResponse(string View, DateTime GeneratedAtUtc, string TimeZone, DateOnly From, DateOnly To,
    string PaymentMode, IReadOnlyList<DashboardRestaurantOption> Restaurants, bool HasMoreRestaurants,
    IReadOnlyList<DashboardMetric> Metrics, DashboardFinancialSummary? Financials, DashboardChart Chart,
    IReadOnlyList<DashboardAlert> Alerts, IReadOnlyList<DashboardBranchRow> BranchRows,
    IReadOnlyList<DashboardSessionRow> SessionRows, int PageIndex, int PageSize, int TotalRows);
