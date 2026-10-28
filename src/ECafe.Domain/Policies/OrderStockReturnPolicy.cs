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
            (order.CancellationRequest.IngredientsUnused != true && order.CancellationRequest.StockReturnMode != OrderStockReturnMode.ByIngredient) ||
            order.CancellationRequest.StockReturnedAt.HasValue || order.CancellationRequest.StockReturnLines.Count > 0 ||
            !OrderCancellationPolicy.IsCancelled(order)) Invalid();
    }

    /// <summary>Yalnız ilkin mənfi sərfiyyat nüsxələrini qaytarır; cari reseptə baxmır, aidiyyət/vahid/miqdar və bütün qalıqları mutasiyadan əvvəl yoxlayır.</summary>
    public static void Validate(Order order, IReadOnlyList<InventoryMovement> consumption, IReadOnlyDictionary<int, InventoryItem> inventory,
        IReadOnlyDictionary<int, decimal>? returns = null)
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
        var totals = ConsumptionTotals(consumption);
        foreach (var group in consumption.GroupBy(row => row.InventoryItemId))
        {
            var total = totals[group.Key];
            var amount = returns == null ? total : returns.GetValueOrDefault(group.Key, -1);
            if (amount < 0 || amount > total || decimal.Round(amount, 6) != amount)
                throw new BusinessRuleException(ErrorCode.OrderStockReturnQuantitiesInvalid);
            if (amount > InventoryStockPolicy.MaximumQuantity - inventory[group.Key].QuantityOnHand) Invalid();
        }
    }

    /// <summary>Eyni ərzağın ayrı sifariş sətirlərindəki ilkin sərfiyyatlarını toplayır; cari resept və sifariş miqdarından istifadə etmir.</summary>
    public static IReadOnlyDictionary<int, decimal> ConsumptionTotals(IReadOnlyList<InventoryMovement> consumption)
    {
        try
        {
            var totals = consumption.GroupBy(row => row.InventoryItemId).ToDictionary(group => group.Key, group => checked(-group.Sum(row => row.QuantityChange)));
            if (totals.Count > OrderStockReturnDecisionPolicy.MaximumIngredients || totals.Values.Any(value => value <= 0 || value > InventoryStockPolicy.MaximumQuantity)) Invalid();
            return totals;
        }
        catch (OverflowException) { Invalid(); throw; }
    }

    /// <summary>Qismən plan bütün ilkin ərzaqları, sıfır qaytarılanlar daxil, bir dəfə göstərməlidir; yad və artıq miqdara icazə verilmir.</summary>
    public static IReadOnlyDictionary<int, decimal> ReturnAmounts(IReadOnlyList<InventoryMovement> consumption,
        IReadOnlyList<ECafe.Domain.ValueObjects.OrderStockReturnQuantity>? quantities)
    {
        var totals = ConsumptionTotals(consumption);
        if (quantities == null) return totals;
        if (quantities.Count != totals.Count || quantities.Any(line => line == null) || quantities.Select(line => line.InventoryItemId).Distinct().Count() != quantities.Count ||
            quantities.Any(line => !totals.TryGetValue(line.InventoryItemId, out var total) || line.ReturnQuantity < 0 ||
                line.ReturnQuantity > total || decimal.Round(line.ReturnQuantity, 6) != line.ReturnQuantity))
            throw new BusinessRuleException(ErrorCode.OrderStockReturnQuantitiesInvalid);
        return quantities.ToDictionary(line => line.InventoryItemId, line => line.ReturnQuantity);
    }

    /// <summary>Yoxlanmış qaytarma miqdarını ilkin mənbələrin ID sırası ilə yuvarlaqlaşdırmadan bölür; hər mənbə ən çox bir dəfə istifadə olunur.</summary>
    public static IReadOnlyList<(InventoryMovement Source, decimal Quantity)> Allocate(IReadOnlyList<InventoryMovement> consumption,
        IReadOnlyDictionary<int, decimal> amounts)
    {
        var remaining = amounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        var result = new List<(InventoryMovement Source, decimal Quantity)>();
        foreach (var source in consumption.OrderBy(row => row.Id))
        {
            var quantity = Math.Min(-source.QuantityChange, remaining.GetValueOrDefault(source.InventoryItemId));
            if (quantity <= 0) continue;
            result.Add((source, quantity));
            remaining[source.InventoryItemId] -= quantity;
        }
        if (remaining.Values.Any(value => value != 0)) Invalid();
        return result;
    }

    /// <summary>Uyğunsuz və ya yarımçıq sərfiyyatda bütün ləğv/qaytarma transaction-ını dayandırır.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new BusinessRuleException(ErrorCode.OrderStockReturnInvalid);
}
