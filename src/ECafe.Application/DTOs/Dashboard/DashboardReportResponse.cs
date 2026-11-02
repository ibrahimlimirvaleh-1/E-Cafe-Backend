namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardReportResponse(string Kind, DateTime GeneratedAtUtc, string TimeZone, DateOnly From, DateOnly To,
    string PaymentMode, IReadOnlyList<DashboardMetric> Metrics, IReadOnlyList<DashboardOrderReportRow> Orders,
    IReadOnlyList<DashboardReservationReportRow> Reservations, IReadOnlyList<DashboardPaymentReportRow> Payments,
    int PageIndex, int PageSize, int TotalRows);
