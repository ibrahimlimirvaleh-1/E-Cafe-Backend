using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.DTOs.Lookup;
using ECafe.Application.DTOs.Order;
using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings.ContentLocalization;

internal static class InventoryResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> All { get; } =
    [
        new ContentResponseProfile<InventoryItemDto>(
            new(ContentEntityType.InventoryItem, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Unit, (r, _) => r.UnitId, r => r.UnitName, v => v.Name)),
        new ContentResponseProfile<RecipeDto>(
            new(ContentEntityType.Item, (r, _) => r.ItemId, r => r.ItemName, v => v.Name),
            new(ContentEntityType.InventoryItem, (r, _) => r.InventoryItemId, r => r.InventoryItemName, v => v.Name),
            new(ContentEntityType.Unit, (r, _) => r.UnitId, r => r.UnitName, v => v.Name)),
        new ContentResponseProfile<InventoryMovementResponse>(
            new ContentResponseField<InventoryMovementResponse>(ContentEntityType.Unit, (r, _) => r.UnitId, r => r.UnitName, v => v.Name)),
        new ContentResponseProfile<InventoryMovementHistoryResponse>(
            new ContentResponseField<InventoryMovementHistoryResponse>(ContentEntityType.Unit, (r, _) => r.UnitId, r => r.UnitName, v => v.Name)),
        new ContentResponseProfile<KitchenStockReturnIngredientResponse>(
            new(ContentEntityType.InventoryItem, (r, _) => r.InventoryItemId, r => r.Name, v => v.Name),
            new(ContentEntityType.Unit, (r, _) => r.UnitId, r => r.UnitName, v => v.Name)),
        new ContentResponseProfile<LookupItemResponse>(
            new ContentResponseField<LookupItemResponse>(ContentEntityType.Unit, (r, c) => c.UnitLookup ? r.Id : null, r => r.Name, v => v.Name))
    ];
}
