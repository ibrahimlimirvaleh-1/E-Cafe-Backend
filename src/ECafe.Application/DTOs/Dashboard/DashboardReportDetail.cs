namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardReportDetail(IReadOnlyList<DashboardReportColumn> Columns, IReadOnlyList<DashboardDetailRow> Rows);
