using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Ləğvi ləğv tarixində, ərzağı isə həmin sifarişin tam sərf/geri dönüş tarixçəsi ilə göstərir; qaytarılmayan hissəni avtomatik maliyyə itkisi adlandırmır.</summary>
    private async Task<DashboardReportData> ReadCancellationReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var consumption = (int)InventoryMovementTypeCode.OrderConsumption;
        var stockReturn = (int)InventoryMovementTypeCode.StockReturn;
        var orders = db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) && order.StatusId == cancelled && order.CancelledAt >= period.StartUtc && order.CancelledAt < period.EndUtc);
        var movements = db.InventoryMovements.AsNoTracking().Where(m => ids.Contains(m.RestaurantId) && m.RelatedOrderId != null && (m.MovementTypeId == consumption || m.MovementTypeId == stockReturn))
            .GroupBy(m => new { m.RestaurantId, m.RelatedOrderId, m.InventoryItemId, Name = m.InventoryItem.Name, m.UnitId, Unit = m.Unit.Code })
            .Select(group => new { group.Key.RestaurantId, group.Key.RelatedOrderId, group.Key.InventoryItemId, group.Key.Name, group.Key.UnitId, group.Key.Unit,
                Consumed = group.Sum(m => m.MovementTypeId == consumption && m.QuantityChange < 0 ? -m.QuantityChange : 0),
                Returned = group.Sum(m => m.MovementTypeId == stockReturn && m.QuantityChange > 0 ? m.QuantityChange : 0) });
        var query = from order in orders
                    join movement in movements on new { order.RestaurantId, Id = (int?)order.Id } equals new { movement.RestaurantId, Id = movement.RelatedOrderId } into stock
                    from movement in stock.DefaultIfEmpty()
                    select new { order.Id, Restaurant = order.Restaurant.Name, Table = order.Table.Name ?? "Masa-" + order.Table.TableNo, order.CancelledAt,
                        Reason = order.CancellationReason ?? (order.CancellationRequest != null ? order.CancellationRequest.Reason : null), Stage = order.PreparingAt.HasValue ? "afterPreparation" : "beforePreparation",
                        Actor = db.Users.Where(user => user.Id == ((order.CancellationRequest != null ? order.CancellationRequest.ResolvedByUserId : null) ?? order.CancelledByUserId)).Select(user => user.Name + " " + user.Surname).FirstOrDefault(),
                        IngredientId = (int?)movement.InventoryItemId, Ingredient = movement.Name, UnitId = (int?)movement.UnitId, Unit = movement.Unit,
                        Consumed = (decimal?)movement.Consumed, Returned = (decimal?)movement.Returned };
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var rows = await query.OrderByDescending(row => row.CancelledAt).ThenByDescending(row => row.Id).ThenBy(row => row.IngredientId).ThenBy(row => row.UnitId).Skip(page.Skip).Take(page.Take).ToListAsync(token);
        return View.Map(count, "orderId:number restaurant table cancelledAt:datetime stage:code reason approvedBy ingredient unit consumed:number returned:number notReturned:number review:code", rows.Select(row =>
            View.Row($"{row.Id}:{row.IngredientId}:{row.UnitId}", row.Id, row.Restaurant, row.Table, row.CancelledAt, row.Stage, row.Reason, row.Actor, row.Ingredient, row.Unit,
                row.Consumed, row.Returned, row.Consumed.HasValue ? Math.Max(0, row.Consumed.Value - (row.Returned ?? 0)) : (decimal?)null, row.Returned > row.Consumed)),
            View.Metric("cancelledOrders", await orders.CountAsync(token)), View.Metric("ingredientRows", count));
    }
}
