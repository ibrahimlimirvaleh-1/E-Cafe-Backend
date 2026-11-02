namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardDetailRow(string Id, IReadOnlyList<DashboardReportCell> Cells);
