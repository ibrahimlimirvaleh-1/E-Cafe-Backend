namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardOrderReportRow(int Id, int RestaurantId, string RestaurantName, string TableName,
    int? TableSessionId, int SourceId, int StatusId, DateTime OpenedAtUtc, DateTime? SentToKitchenAtUtc,
    DateTime? ServedAtUtc, int ItemQuantity, decimal ItemSubtotal, decimal ConfirmedAmount);
