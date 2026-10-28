using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings;

public static class OrderCancellationStockReturnLineMapper
{
    /// <summary>Yoxlanmış ərzağın ilkin sərfiyyat və ilk qaytarma miqdarını dəyişməz qərar sətrinə çevirir.</summary>
    public static OrderCancellationStockReturnLine Map(InventoryItem item, decimal consumed, decimal returned)
        => new() { InventoryItemId = item.Id, UnitId = item.UnitId, ConsumedQuantity = consumed, ReturnQuantity = returned };
}
