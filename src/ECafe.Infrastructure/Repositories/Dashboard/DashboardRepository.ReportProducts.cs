using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Təqdim edilmiş məhsulu təqdim vaxtında sayır; yalnız sahibkara tam tarixi maya və brüt qazanc göstərir.</summary>
    private async Task<DashboardReportData> ReadProductReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var served = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served);
        var closed = StatusIds.Build(StatusType.Order, (int)OrderStatus.Closed);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var includeCosts = scope.Role == RoleCode.Owner;
        var lines = db.OrderItems.AsNoTracking().Where(line => ids.Contains(line.Order.RestaurantId) && !line.Order.IsDeleted && line.Quantity > 0 &&
            ((line.Order.StatusId == served || line.Order.StatusId == closed) && line.Order.ServedAt >= period.StartUtc && line.Order.ServedAt < period.EndUtc ||
            line.Order.StatusId == cancelled && line.Order.CancelledAt >= period.StartUtc && line.Order.CancelledAt < period.EndUtc));
        var query = lines.GroupBy(line => new { line.Order.RestaurantId, Restaurant = line.Order.Restaurant.Name, line.ItemId, line.NameSnapshot })
            .Select(group => new { group.Key.RestaurantId, group.Key.Restaurant, group.Key.ItemId, group.Key.NameSnapshot,
                Sold = group.Sum(line => line.Order.StatusId == cancelled ? 0 : line.Quantity),
                Cancelled = group.Sum(line => line.Order.StatusId == cancelled ? line.Quantity : 0),
                Amount = group.Sum(line => line.Order.StatusId == cancelled ? 0 : line.LineTotal),
                MissingCost = includeCosts && group.Any(line => line.Order.StatusId != cancelled && !line.IngredientCostSnapshot.HasValue),
                Cost = includeCosts ? group.Sum(line => line.Order.StatusId == cancelled ? 0 : line.IngredientCostSnapshot ?? 0) : 0m });
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var totals = await query.GroupBy(_ => 1).Select(group => new { Sold = group.Sum(row => row.Sold), Cancelled = group.Sum(row => row.Cancelled) }).SingleOrDefaultAsync(token);
        var rows = await query.OrderByDescending(row => row.Sold).ThenBy(row => row.RestaurantId).ThenBy(row => row.ItemId).ThenBy(row => row.NameSnapshot)
            .Skip(page.Skip).Take(page.Take).ToListAsync(token);
        const string columns = "restaurant product sold:number cancelled:number deliveredAmount:currency averagePrice:currency";
        return View.Map(count, includeCosts ? columns + " ingredientCost:currency grossProfit:currency averageIngredientCost:number" : columns, rows.Select(row =>
        {
            object?[] cells = [row.Restaurant, row.NameSnapshot, row.Sold, row.Cancelled, row.Amount,
                row.Sold > 0 ? decimal.Round(row.Amount / row.Sold, 2) : (decimal?)null];
            if (includeCosts)
            {
                decimal? cost = row.Sold > 0 && !row.MissingCost ? decimal.Round(row.Cost, 2, MidpointRounding.AwayFromZero) : null;
                cells = [.. cells, cost, row.Amount - cost, cost.HasValue ? InventoryCostPolicy.RoundAmount(row.Cost / row.Sold) : null];
            }
            return View.Row($"{row.RestaurantId}:{row.ItemId}:{row.NameSnapshot}", cells);
        }),
            View.Metric("productRows", count), View.Metric("soldUnits", totals?.Sold ?? 0), View.Metric("cancelledUnits", totals?.Cancelled ?? 0));
    }
}
