namespace ECafe.Application.DTOs.Dashboard;

public sealed record DashboardFinancialSummary(decimal CollectedAmount, decimal RefundedAmount, decimal NetCollectedAmount,
    decimal DepositCollectedAmount, decimal SessionCollectedAmount);
