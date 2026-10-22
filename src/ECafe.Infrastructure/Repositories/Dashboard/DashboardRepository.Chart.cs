using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Hadisələri UTC-nin 15 dəqiqəlik hissələrində aqreqasiya edir; yarımsaatlıq saat qurşaqlarında gün sərhədi itmir və ən çox 92 günlük hissələr yüklənir.</summary>
    public async Task<IReadOnlyList<DashboardHour>> GetChartAsync(DashboardScope scope, DashboardPeriod period, CancellationToken cancellationToken)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        if (scope.Role == RoleCode.SuperAdmin)
            return await db.Reservations.AsNoTracking().Where(reservation => ids.Contains(reservation.RestaurantId) &&
                    reservation.ReservedAt >= period.StartUtc && reservation.ReservedAt < period.EndUtc)
                .GroupBy(reservation => new { Date = reservation.ReservedAt.Date, Hour = reservation.ReservedAt.Hour,
                    Part = reservation.ReservedAt.Minute / DashboardPeriodPolicy.ChartBucketMinutes })
                .Select(group => new DashboardHour(group.Key.Date, group.Key.Hour, group.Count(), group.Key.Part * DashboardPeriodPolicy.ChartBucketMinutes)).ToListAsync(cancellationToken);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        return await db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) &&
                order.OpenedAt >= period.StartUtc && order.OpenedAt < period.EndUtc && order.StatusId != cancelled)
            .GroupBy(order => new { Date = order.OpenedAt.Date, Hour = order.OpenedAt.Hour,
                Part = order.OpenedAt.Minute / DashboardPeriodPolicy.ChartBucketMinutes })
            .Select(group => new DashboardHour(group.Key.Date, group.Key.Hour, scope.Role == RoleCode.Owner
                ? group.Sum(order => order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (decimal?)line.LineTotal) ?? 0)
                : group.Count(), group.Key.Part * DashboardPeriodPolicy.ChartBucketMinutes)).ToListAsync(cancellationToken);
    }
}
