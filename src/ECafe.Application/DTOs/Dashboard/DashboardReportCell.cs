namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardReportCell(string? Text = null, decimal? Number = null, DateTime? Utc = null);
