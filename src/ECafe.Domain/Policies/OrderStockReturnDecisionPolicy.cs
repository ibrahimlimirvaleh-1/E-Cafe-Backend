using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.ValueObjects;

namespace ECafe.Domain.Policies;

public static class OrderStockReturnDecisionPolicy
{
    public const int MaximumIngredients = 2000;

    /// <summary>Köhnə boolean müqaviləsini saxlayır; yeni seçimlə qarışdırılmasını, dublikat/mənfi və altı rəqəmdən dəqiq miqdarları rədd edir.</summary>
    public static OrderStockReturnDecision Normalize(bool approved, bool? legacyUnused, OrderStockReturnMode? mode,
        IReadOnlyList<OrderStockReturnQuantity>? quantities)
    {
        if (!approved)
        {
            if (legacyUnused == true || mode.HasValue || quantities?.Count > 0) Invalid();
            return new(OrderStockReturnMode.Used, []);
        }
        if (mode.HasValue && legacyUnused.HasValue || mode.HasValue && !Enum.IsDefined(mode.Value)) Invalid();
        var selected = mode ?? (legacyUnused == true ? OrderStockReturnMode.Unused : OrderStockReturnMode.Used);
        if (selected != OrderStockReturnMode.ByIngredient)
        {
            if (quantities?.Count > 0) Invalid();
            return new(selected, []);
        }
        if (quantities == null || quantities.Count == 0 || quantities.Count > MaximumIngredients || quantities.Any(line => line == null) ||
            quantities.Select(line => line.InventoryItemId).Distinct().Count() != quantities.Count ||
            quantities.Any(line => line.InventoryItemId <= 0 || line.ReturnQuantity < 0 ||
                line.ReturnQuantity > InventoryStockPolicy.MaximumQuantity || decimal.Round(line.ReturnQuantity, 6) != line.ReturnQuantity)) Invalid();
        return new(selected, quantities!.OrderBy(line => line.InventoryItemId).ToArray());
    }

    /// <summary>İlk təsdiqin rejim və bütün ərzaq miqdarlarını dəyişməz saxlayır; eyni planın sırası fərqli olan təkrarı təhlükəsizdir.</summary>
    public static void EnsureReplayMatches(OrderCancellationRequest? request, OrderStockReturnDecision decision)
    {
        if (request?.StatusId != OrderCancellationRequestStatus.Approved || !request.ResolvedAt.HasValue) return;
        var stored = request.StockReturnMode ?? (request.IngredientsUnused == true ? OrderStockReturnMode.Unused : OrderStockReturnMode.Used);
        if (stored != decision.Mode || stored == OrderStockReturnMode.ByIngredient &&
            !request.StockReturnLines.OrderBy(line => line.InventoryItemId).Select(line => new OrderStockReturnQuantity(line.InventoryItemId, line.ReturnQuantity))
                .SequenceEqual(decision.Returns)) throw new BusinessRuleException(ErrorCode.OrderCancellationRequestConflict);
    }

    /// <summary>Səhv miqdarda status və anbar dəyişikliklərindən əvvəl bütün əməliyyatı dayandırır.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new BusinessRuleException(ErrorCode.OrderStockReturnQuantitiesInvalid);
}
