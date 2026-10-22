namespace ECafe.Application.DTOs.Dashboard;

public sealed class DashboardRequest
{
    public int? RestaurantId { get; set; }
    public string Period { get; set; } = "week";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string PaymentMode { get; set; } = "real";
    public int PageIndex { get; set; } = 1;
}
