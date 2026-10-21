using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class OrderStockMovementMapper
{
    /// <summary>Hesablanmış resept istehlakını sifariş sətri və anbar vahidi ilə əlaqəli dəyişməz uçot hərəkətinə çevirir.</summary>
    public static InventoryMovement Map(Order order, OrderStockConsumption row) => new()
    {
        RestaurantId = order.RestaurantId, InventoryItemId = row.InventoryItemId, UnitId = row.UnitId,
        MovementTypeId = (int)InventoryMovementTypeCode.OrderConsumption, QuantityChange = -row.Quantity,
        RelatedOrderId = order.Id, RelatedOrderItemId = row.OrderItemId
    };
}
