using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Yaradılma tarixinə görə sifariş hesabatını aqreqasiya edir; gözləyən və ləğv edilmiş sifarişin məbləğini təsdiqlənmiş cəmə əlavə etmir.</summary>
    private async Task<DashboardReportData> ReadOrderReportAsync(DashboardScope scope, DashboardPeriod period,
        int? statusId, int pageIndex, bool export, CancellationToken cancellationToken)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var pending = StatusIds.Build(StatusType.Order, (int)OrderStatus.PendingWaiterConfirmation);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var query = db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) &&
            order.OpenedAt >= period.StartUtc && order.OpenedAt < period.EndUtc && (!statusId.HasValue || order.StatusId == statusId));
        var totals = await query.GroupBy(_ => 1).Select(group => new DashboardReportTotals
        {
            RowCount = group.Count(), PendingCount = group.Count(order => order.StatusId == pending),
            CancelledCount = group.Count(order => order.StatusId == cancelled),
            ItemQuantity = group.Sum(order => order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (int?)line.Quantity) ?? 0),
            ConfirmedAmount = scope.Role == RoleCode.Manager ? 0 : group.Sum(order => order.StatusId != pending && order.StatusId != cancelled
                ? order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (decimal?)line.LineTotal) ?? 0 : 0)
        }).SingleOrDefaultAsync(cancellationToken) ?? new DashboardReportTotals();
        var page = ReportPage(totals.RowCount, pageIndex, export);
        var rows = await query.OrderByDescending(order => order.OpenedAt).ThenByDescending(order => order.Id)
            .Skip(page.Skip).Take(page.Take).Select(order => new DashboardOrderReportRow(
                order.Id, order.RestaurantId, order.Restaurant.Name,
                string.IsNullOrWhiteSpace(order.Table.Name) ? "Masa-" + order.Table.TableNo : order.Table.Name, order.TableSessionId,
                order.SourceId, order.StatusId, order.OpenedAt, order.SentToKitchenAt, order.ServedAt,
                order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (int?)line.Quantity) ?? 0,
                order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (decimal?)line.LineTotal) ?? 0,
                order.StatusId == pending || order.StatusId == cancelled ? 0 :
                    order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (decimal?)line.LineTotal) ?? 0))
            .ToListAsync(cancellationToken);
        return new(totals, rows, [], []);
    }
}
