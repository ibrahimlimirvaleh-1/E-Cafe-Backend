namespace ECafe.Application.DTOs.Dashboard;

public sealed class DashboardReportRequest : DashboardRequest
{
    public string Kind { get; set; } = "orders";
    public int? StatusId { get; set; }
    public string PaymentEvent { get; set; } = "all";
}
