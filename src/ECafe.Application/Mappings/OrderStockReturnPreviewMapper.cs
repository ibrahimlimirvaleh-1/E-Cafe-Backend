using ECafe.Application.DTOs.Order;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings;

public static class OrderStockReturnPreviewMapper
{
    /// <summary>Yoxlanmış ilkin sərfiyyatı ərzaq üzrə qruplaşdırılmış, qiymətsiz mətbəx seçiminə çevirir.</summary>
    public static KitchenStockReturnPreviewResponse Map(int orderId, IReadOnlyList<InventoryMovement> consumption,
        IReadOnlyDictionary<int, decimal> totals)
        => new(orderId, consumption.GroupBy(row => row.InventoryItemId).OrderBy(group => group.Key).Select(group =>
            new KitchenStockReturnIngredientResponse(group.Key, group.First().InventoryItem.Name, group.First().UnitId,
                group.First().Unit.Name, group.First().Unit.Code, totals[group.Key])).ToArray());
}
