namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardBranchRow(int RestaurantId, string Name, string? BranchName, bool IsActive, int ReservationCount,
    int OrderCount, decimal OrderAmount, decimal CollectedAmount, decimal RefundedAmount, int? ContractId, int? ContractStatusId, DateTime? ContractEndsAt);
