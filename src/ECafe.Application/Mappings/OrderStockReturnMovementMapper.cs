using ECafe.Domain.Entities;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings;

public static class OrderStockReturnMovementMapper
{
    /// <summary>İlkin sərfiyyatı silmədən eyni vahiddə əks müsbət uçot yaradır; unikal mənbə ID-si ikiqat qaytarmanı bloklayır.</summary>
    public static InventoryMovement Map(InventoryMovement source, decimal? returnQuantity = null) => new()
    {
        RestaurantId = source.RestaurantId, InventoryItemId = source.InventoryItemId, UnitId = source.UnitId,
        QuantityChange = returnQuantity ?? -source.QuantityChange, MovementTypeId = (int)InventoryMovementTypeCode.StockReturn,
        RelatedOrderId = source.RelatedOrderId, RelatedOrderItemId = source.RelatedOrderItemId, ReversedMovementId = source.Id
    };
}
