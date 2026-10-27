using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class OrderStockReturnPolicy
{
    /// <summary>Stok yalnız istifadə olunmamış ərzaq üçün yeni təsdiqlənən ləğvdə qaytarılır; əvvəl qaytarılmış qərar ikinci dəfə icra edilmir.</summary>
    public static void EnsureCanRestore(Order order)
    {
        if (order.CancellationRequest?.StatusId != OrderCancellationRequestStatus.Approved ||
            order.CancellationRequest.IngredientsUnused != true || order.CancellationRequest.StockReturnedAt.HasValue ||
            !OrderCancellationPolicy.IsCancelled(order)) Invalid();
    }

    /// <summary>Yalnız ilkin mənfi sərfiyyat nüsxələrini qaytarır; cari reseptə baxmır, aidiyyət/vahid/miqdar və bütün qalıqları mutasiyadan əvvəl yoxlayır.</summary>
    public static void Validate(Order order, IReadOnlyList<InventoryMovement> consumption, IReadOnlyDictionary<int, InventoryItem> inventory)
    {
        if (consumption.Count == 0 || consumption.Select(row => row.Id).Distinct().Count() != consumption.Count ||
            order.OrderItems.Any(line => !line.IsDeleted && !consumption.Any(row => row.RelatedOrderItemId == line.Id))) Invalid();
        foreach (var row in consumption)
        {
            if (row.Id <= 0 || row.RestaurantId != order.RestaurantId || row.RelatedOrderId != order.Id ||
                row.MovementTypeId != (int)InventoryMovementTypeCode.OrderConsumption || row.ReversedMovementId.HasValue ||
                row.QuantityChange >= 0 || row.QuantityChange < -InventoryStockPolicy.MaximumQuantity ||
                decimal.Round(row.QuantityChange, 6) != row.QuantityChange || row.RelatedOrderItem == null ||
                row.RelatedOrderItem.OrderId != order.Id || row.RelatedOrderItemId != row.RelatedOrderItem.Id ||
                !inventory.TryGetValue(row.InventoryItemId, out var item) || item.IsDeleted ||
                item.RestaurantId != order.RestaurantId || item.UnitId != row.UnitId || item.QuantityOnHand < 0 ||
                item.QuantityOnHand > InventoryStockPolicy.MaximumQuantity) Invalid();
        }
        foreach (var group in consumption.GroupBy(row => row.InventoryItemId))
        {
            decimal amount;
            try { amount = checked(-group.Sum(row => row.QuantityChange)); }
            catch (OverflowException) { Invalid(); throw; }
            if (amount > InventoryStockPolicy.MaximumQuantity - inventory[group.Key].QuantityOnHand) Invalid();
        }
    }

    /// <summary>Uyğunsuz və ya yarımçıq sərfiyyatda bütün ləğv/qaytarma transaction-ını dayandırır.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new BusinessRuleException(ErrorCode.OrderStockReturnInvalid);
}
