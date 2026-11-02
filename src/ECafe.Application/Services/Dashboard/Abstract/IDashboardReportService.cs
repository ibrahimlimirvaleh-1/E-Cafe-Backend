using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Dashboard.Abstract;

public interface IDashboardReportService
{
    Task<DashboardReportResponse> GetAsync(RoleCode role, DashboardReportRequest request, CancellationToken cancellationToken);
    Task<DashboardReportFile> ExportAsync(RoleCode role, DashboardReportRequest request, CancellationToken cancellationToken);
}
