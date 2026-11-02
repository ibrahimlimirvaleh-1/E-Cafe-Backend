using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Policies;
using ECafe.Domain.Enums;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Cari qalığı dövr hərəkətlərindən ayırır; ilkin qalıq tarixçəsi olmadığı üçün keçmiş açılış/bağlanış qalığını uydurmur və fərqli vahidləri toplamır.</summary>
    private async Task<DashboardReportData> ReadStockReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var items = db.InventoryItems.AsNoTracking().Where(item => ids.Contains(item.RestaurantId));
        var language = TranslationPolicy.LanguageId(CultureInfo.CurrentUICulture.Name);
        var consumption = (int)InventoryMovementTypeCode.OrderConsumption;
        var stockReturn = (int)InventoryMovementTypeCode.StockReturn;
        var waste = (int)InventoryMovementTypeCode.Waste;
        var movements = db.InventoryMovements.AsNoTracking().Where(m => ids.Contains(m.RestaurantId) && m.CreatedAt >= period.StartUtc && m.CreatedAt < period.EndUtc);
        var grouped = movements.GroupBy(m => new { m.RestaurantId, m.InventoryItemId, m.UnitId }).Select(group => new
        {
            group.Key.RestaurantId, group.Key.InventoryItemId, group.Key.UnitId,
            In = group.Sum(m => m.QuantityChange > 0 ? m.QuantityChange : 0),
            Out = group.Sum(m => m.QuantityChange < 0 ? -m.QuantityChange : 0),
            Consumed = group.Sum(m => m.MovementTypeId == consumption && m.QuantityChange < 0 ? -m.QuantityChange : 0),
            Returned = group.Sum(m => m.MovementTypeId == stockReturn && m.QuantityChange > 0 ? m.QuantityChange : 0),
            Waste = group.Sum(m => m.MovementTypeId == waste && m.QuantityChange < 0 ? -m.QuantityChange : 0)
        });
        var count = await items.CountAsync(token);
        var low = await items.CountAsync(item => item.IsActive && item.QuantityOnHand <= item.LowStockThreshold, token);
        var outOfStock = await items.CountAsync(item => item.IsActive && item.QuantityOnHand <= 0, token);
        var page = ReportPage(count, pageIndex, export);
        var query = from item in items
                    join movement in grouped on new { item.RestaurantId, InventoryItemId = item.Id, item.UnitId } equals new { movement.RestaurantId, movement.InventoryItemId, movement.UnitId } into history
                    from movement in history.DefaultIfEmpty()
                    select new
                    {
                        item.Id, item.RestaurantId, Restaurant = item.Restaurant.Name,
                        Name = item.Translations.Where(t => t.LanguageId == language).Select(t => t.Name).FirstOrDefault() ?? item.Name,
                        Unit = item.Unit.Code, item.QuantityOnHand, item.LowStockThreshold, item.IsActive,
                        In = (decimal?)movement.In ?? 0, Out = (decimal?)movement.Out ?? 0, Consumed = (decimal?)movement.Consumed ?? 0,
                        Returned = (decimal?)movement.Returned ?? 0, Waste = (decimal?)movement.Waste ?? 0,
                        UnitMismatch = movements.Any(m => m.RestaurantId == item.RestaurantId && m.InventoryItemId == item.Id && m.UnitId != item.UnitId)
                    };
        var rows = await query.OrderBy(item => item.QuantityOnHand > item.LowStockThreshold).ThenBy(item => item.RestaurantId).ThenBy(item => item.Name).ThenBy(item => item.Id)
            .Skip(page.Skip).Take(page.Take).ToListAsync(token);
        return View.Map(count, "restaurant ingredient unit currentStock:number threshold:number incoming:number outgoing:number consumed:number returned:number waste:number stockState:code review:code", rows.Select(row =>
            View.Row(row.Id.ToString(), row.Restaurant, row.Name, row.Unit, row.QuantityOnHand, row.LowStockThreshold, row.In, row.Out, row.Consumed, row.Returned, row.Waste,
                !row.IsActive ? "inactive" : row.QuantityOnHand <= 0 ? "out" : row.QuantityOnHand <= row.LowStockThreshold ? "low" : "available", row.UnitMismatch)),
            View.Metric("stockItems", count), View.Metric("lowStock", low), View.Metric("outOfStock", outOfStock));
    }
}
