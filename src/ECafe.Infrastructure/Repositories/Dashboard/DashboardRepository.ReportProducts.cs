using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Təqdim edilmiş məhsulu təqdim vaxtında, ləğvi ləğv vaxtında sayır; ad/qiymət snapshot-ını və filialı saxlayır, pul daxilolması yaratmır.</summary>
    private async Task<DashboardReportData> ReadProductReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var served = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served);
        var closed = StatusIds.Build(StatusType.Order, (int)OrderStatus.Closed);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var lines = db.OrderItems.AsNoTracking().Where(line => ids.Contains(line.Order.RestaurantId) && !line.Order.IsDeleted && line.Quantity > 0 &&
            ((line.Order.StatusId == served || line.Order.StatusId == closed) && line.Order.ServedAt >= period.StartUtc && line.Order.ServedAt < period.EndUtc ||
            line.Order.StatusId == cancelled && line.Order.CancelledAt >= period.StartUtc && line.Order.CancelledAt < period.EndUtc));
        var query = lines.GroupBy(line => new { line.Order.RestaurantId, Restaurant = line.Order.Restaurant.Name, line.ItemId, line.NameSnapshot })
            .Select(group => new { group.Key.RestaurantId, group.Key.Restaurant, group.Key.ItemId, group.Key.NameSnapshot,
                Sold = group.Sum(line => line.Order.StatusId == cancelled ? 0 : line.Quantity),
                Cancelled = group.Sum(line => line.Order.StatusId == cancelled ? line.Quantity : 0),
                Amount = group.Sum(line => line.Order.StatusId == cancelled ? 0 : line.LineTotal) });
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var totals = await query.GroupBy(_ => 1).Select(group => new { Sold = group.Sum(row => row.Sold), Cancelled = group.Sum(row => row.Cancelled) }).SingleOrDefaultAsync(token);
        var rows = await query.OrderByDescending(row => row.Sold).ThenBy(row => row.RestaurantId).ThenBy(row => row.ItemId).ThenBy(row => row.NameSnapshot)
            .Skip(page.Skip).Take(page.Take).ToListAsync(token);
        return View.Map(count, "restaurant product sold:number cancelled:number deliveredAmount:currency averagePrice:currency", rows.Select(row =>
            View.Row($"{row.RestaurantId}:{row.ItemId}:{row.NameSnapshot}", row.Restaurant, row.NameSnapshot, row.Sold, row.Cancelled, row.Amount,
                row.Sold > 0 ? decimal.Round(row.Amount / row.Sold, 2) : (decimal?)null)),
            View.Metric("productRows", count), View.Metric("soldUnits", totals?.Sold ?? 0), View.Metric("cancelledUnits", totals?.Cancelled ?? 0));
    }
}
