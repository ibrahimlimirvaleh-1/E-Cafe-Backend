using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Repositories.Dashboard;

public interface IDashboardReportRepository
{
    Task<DashboardReportData> GetReportAsync(DashboardScope scope, DashboardPeriod period, DashboardReportKind kind,
        int? statusId, string paymentEvent, int pageIndex, bool export, CancellationToken cancellationToken);
}
