using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class OrderStockMovementMapper
{
    /// <summary>Resept istehlakını və onun həmin andakı mayasını dəyişməz stok hərəkətinə çevirir.</summary>
    public static InventoryMovement Map(Order order, OrderStockConsumption row, decimal? costAmount = null) => new()
    {
        RestaurantId = order.RestaurantId, InventoryItemId = row.InventoryItemId, UnitId = row.UnitId,
        MovementTypeId = (int)InventoryMovementTypeCode.OrderConsumption, QuantityChange = -row.Quantity,
        CostAmount = costAmount,
        RelatedOrderId = order.Id, RelatedOrderItemId = row.OrderItemId
    };
}
