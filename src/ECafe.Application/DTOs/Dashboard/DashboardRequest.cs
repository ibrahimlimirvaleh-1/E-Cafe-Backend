using ECafe.Domain.Constants;

namespace ECafe.Application.DTOs.Dashboard;

public class DashboardRequest
{
    public int? RestaurantId { get; set; }
    public string Period { get; set; } = DashboardFilterCodes.Period.Week;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string PaymentMode { get; set; } = DashboardFilterCodes.PaymentMode.Real;
    public int PageIndex { get; set; } = 1;
}
