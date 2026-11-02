namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardPaymentReportRow(int Id, string Event, int RestaurantId, string RestaurantName,
    DateTime OccurredAtUtc, decimal Amount, string Purpose, int? ReservationId, int? TableSessionId,
    int StatusId, bool IsTest);
