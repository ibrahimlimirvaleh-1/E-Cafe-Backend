using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class OrderStockReturnMovementMapper
{
    /// <summary>İlkin sərfiyyatı silmədən eyni vahiddə əks uçot yaradır və tarixi mayanın qaytarılan hissəsini saxlayır.</summary>
    public static InventoryMovement Map(InventoryMovement source, decimal? returnQuantity = null) => new()
    {
        RestaurantId = source.RestaurantId, InventoryItemId = source.InventoryItemId, UnitId = source.UnitId,
        QuantityChange = returnQuantity ?? -source.QuantityChange, MovementTypeId = (int)InventoryMovementTypeCode.StockReturn,
        CostAmount = InventoryCostPolicy.Allocate(source.CostAmount, returnQuantity ?? -source.QuantityChange, -source.QuantityChange),
        RelatedOrderId = source.RelatedOrderId, RelatedOrderItemId = source.RelatedOrderItemId, ReversedMovementId = source.Id
    };
}
