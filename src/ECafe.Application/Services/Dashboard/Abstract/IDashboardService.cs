using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Dashboard.Abstract;

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(RoleCode role, DashboardRequest request, CancellationToken cancellationToken);
}
