namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardReservationReportRow(int Id, int RestaurantId, string RestaurantName, string TableName,
    int StatusId, DateTime ReservedAtUtc, int PeopleCount, decimal DepositAmount, DateTime? ArrivedAtUtc, DateTime? SeatedAtUtc);
