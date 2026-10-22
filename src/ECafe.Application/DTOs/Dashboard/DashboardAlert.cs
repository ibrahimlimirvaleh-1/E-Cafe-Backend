namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardAlert(string Key, int Count, string Tone, int? RestaurantId = null);
