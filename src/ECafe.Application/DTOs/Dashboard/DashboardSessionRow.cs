namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardSessionRow(int SessionId, int RestaurantId, int TableId, string TableName, string? WaiterName,
    int? ReservationId, int? GuestCount, DateTime OpenedAt, decimal OrderAmount, decimal RemainingAmount,
    int PreparingCount, int ReadyCount, bool HasPendingPayment, bool RequiresReview);
