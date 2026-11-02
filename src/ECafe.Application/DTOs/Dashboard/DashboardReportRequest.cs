using ECafe.Domain.Constants;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.DTOs.Dashboard;

public sealed class DashboardReportRequest : DashboardRequest
{
    public int DayStartHour { get; set; }
    public string Kind { get; set; } = DashboardReportCatalog.Get(DashboardReportKind.Orders).Code;
    public int? StatusId { get; set; }
    public string PaymentEvent { get; set; } = DashboardFilterCodes.PaymentEvent.All;
}
