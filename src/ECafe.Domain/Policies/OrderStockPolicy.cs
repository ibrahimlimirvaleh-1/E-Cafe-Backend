using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public sealed record OrderStockConsumption(int OrderItemId, int InventoryItemId, int UnitId, decimal Quantity);

public static class OrderStockPolicy
{
    /// <summary>Hər aktiv sifariş sətrinə tam resept tələb edir; vahid çevirməsini və bütün inqrediyentlərin cəmi qalığını mutasiyadan əvvəl yoxlayır.</summary>
    public static IReadOnlyList<OrderStockConsumption> BuildPlan(Order order, IReadOnlyCollection<Recipe> recipes,
        IReadOnlyDictionary<int, InventoryItem> inventory)
    {
        var result = new List<OrderStockConsumption>();
        var lines = order.OrderItems.Where(line => !line.IsDeleted).OrderBy(line => line.Id).ToList();
        if (lines.Count == 0 || lines.Any(line => line.Id <= 0 || line.Quantity <= 0)) InvalidRecipe();
        foreach (var line in lines)
        {
            var ingredients = recipes.Where(recipe => recipe.ItemId == line.ItemId && !recipe.IsDeleted && recipe.IsActive).ToList();
            if (ingredients.Count == 0) InvalidRecipe();
            foreach (var group in ingredients.GroupBy(recipe => recipe.InventoryItemId))
            {
                if (!inventory.TryGetValue(group.Key, out var item) || item.IsDeleted || !item.IsActive ||
                    item.RestaurantId != order.RestaurantId || item.Unit == null) InvalidRecipe();
                decimal quantity = 0;
                foreach (var recipe in group)
                {
                    if (recipe.RestaurantId != order.RestaurantId || recipe.Unit == null || recipe.Item == null ||
                        recipe.Item.IsDeleted || recipe.Item.RestaurantId != order.RestaurantId)
                        InvalidRecipe();
                    try { quantity = checked(quantity + InventoryStockPolicy.ConvertQuantity(
                        checked(recipe.Quantity * line.Quantity), recipe.Unit!, item!.Unit)); }
                    catch (Exception error) when (error is OverflowException or BusinessRuleException) { InvalidRecipe(); }
                }
                result.Add(new(line.Id, group.Key, item!.UnitId, InventoryStockPolicy.RoundConsumption(quantity)));
            }
        }
        foreach (var total in result.GroupBy(row => row.InventoryItemId).OrderBy(group => group.Key))
        {
            var item = inventory[total.Key];
            var required = total.Sum(row => row.Quantity);
            if (required > item.QuantityOnHand)
                throw new BusinessRuleException(ErrorCode.KitchenOrderStockInsufficient,
                    new { Ingredient = item.Name, Required = required, Available = item.QuantityOnHand, Unit = item.Unit.Code });
        }
        return result;
    }

    /// <summary>Yarımçıq resepti vahid biznes xətası ilə dayandırır; stokun səssiz ötürülməsinə icazə vermir.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void InvalidRecipe() => throw new BusinessRuleException(ErrorCode.KitchenOrderRecipeInvalid);
}
