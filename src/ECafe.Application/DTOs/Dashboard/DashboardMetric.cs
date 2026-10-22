namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardMetric(string Key, decimal Value, string Unit, decimal? PreviousValue, decimal? ChangePercent);
