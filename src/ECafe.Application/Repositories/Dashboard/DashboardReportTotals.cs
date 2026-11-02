namespace ECafe.Application.Repositories.Dashboard;

public sealed record DashboardReportTotals
{
    public int RowCount { get; init; }
    public int PendingCount { get; init; }
    public int CancelledCount { get; init; }
    public int ItemQuantity { get; init; }
    public int GuestCount { get; init; }
    public int ArrivedCount { get; init; }
    public int SeatedCount { get; init; }
    public decimal ConfirmedAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public int ReceiptCount { get; init; }
    public int RefundCount { get; init; }
}
