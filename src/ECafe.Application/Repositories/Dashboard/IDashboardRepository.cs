using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Repositories.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardAccess?> ResolveAccessAsync(DashboardScope scope, CancellationToken cancellationToken);
    Task<DashboardTotals> GetTotalsAsync(DashboardScope scope, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<DashboardHour>> GetChartAsync(DashboardScope scope, DashboardPeriod period, CancellationToken cancellationToken);
    Task<DashboardLive> GetLiveAsync(DashboardScope scope, DateTime nowUtc, DateTime todayStartUtc, DateTime todayEndUtc, CancellationToken cancellationToken);
    Task<DashboardPage<DashboardBranchRow>> GetBranchesAsync(DashboardScope scope, DashboardPeriod period, DateTime nowUtc, int pageIndex, CancellationToken cancellationToken);
    Task<DashboardPage<Domain.Entities.TableSession>> GetSessionsAsync(DashboardScope scope, int pageIndex, CancellationToken cancellationToken);
}
