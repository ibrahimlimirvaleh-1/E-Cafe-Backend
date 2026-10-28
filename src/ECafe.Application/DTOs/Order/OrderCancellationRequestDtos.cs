using ECafe.Domain.Enums;

namespace ECafe.Application.DTOs.Order;

public sealed record OrderCancellationRequestResponse(int Id, int StatusId, string Reason, DateTimeOffset RequestedAt,
    DateTimeOffset? ForwardedAt, DateTimeOffset? ResolvedAt, string? DecisionReason,
    bool? IngredientsUnused = null, DateTimeOffset? StockReturnedAt = null, OrderStockReturnMode? StockReturnMode = null);

public sealed record DecideOrderCancellationRequest(bool? Approved, string Reason, bool? IngredientsUnused = null,
    OrderStockReturnMode? StockReturnMode = null, IReadOnlyList<OrderStockReturnQuantityRequest>? StockReturns = null);

public sealed record OrderStockReturnQuantityRequest(int InventoryItemId, decimal ReturnQuantity);
public sealed record OrderStockReturnQuantityResponse(int InventoryItemId, decimal ReturnQuantity)
{
    public string ReturnQuantityText => ReturnQuantity.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}
public sealed record KitchenStockReturnIngredientResponse(int InventoryItemId, string Name, int UnitId,
    string UnitName, string UnitCode, decimal ConsumedQuantity)
{
    public string ConsumedQuantityText => ConsumedQuantity.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}
public sealed record KitchenStockReturnPreviewResponse(int OrderId, IReadOnlyList<KitchenStockReturnIngredientResponse> Ingredients);
