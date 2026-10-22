namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardPoint(DateTime BucketStartUtc, decimal Value);

public sealed record DashboardChart(string Key, string Unit, string Granularity, IReadOnlyList<DashboardPoint> Points);
