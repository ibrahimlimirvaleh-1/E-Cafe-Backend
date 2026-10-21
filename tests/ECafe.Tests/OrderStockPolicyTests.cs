using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class OrderStockPolicyTests
{
    [Fact]
    public void MultipliesPortionsAndConvertsGramsToStockKilogramsWithoutMutating()
    {
        var (order, recipes, stock) = Create();
        var row = Assert.Single(OrderStockPolicy.BuildPlan(order, recipes, stock));
        Assert.Equal(0.5m, row.Quantity);
        Assert.Equal(1, row.UnitId);
        Assert.Equal(7, row.OrderItemId);
        Assert.Equal(1m, stock[1].QuantityOnHand);
    }

    [Fact]
    public void AggregatesSharedIngredientsAcrossDifferentOrderLinesBeforeCheckingStock()
    {
        var (order, recipes, stock) = Create();
        order.OrderItems.Add(new OrderItem { Id = 8, ItemId = 1, Quantity = 3 });
        var error = Assert.Throws<BusinessRuleException>(() => OrderStockPolicy.BuildPlan(order, recipes, stock));
        Assert.Equal(ErrorCode.KitchenOrderStockInsufficient, error.Code);
        Assert.Equal(1m, stock[1].QuantityOnHand);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactiveRecipe")]
    [InlineData("inactiveIngredient")]
    [InlineData("deletedIngredient")]
    [InlineData("otherRestaurant")]
    [InlineData("wrongUnit")]
    [InlineData("zeroRate")]
    [InlineData("zeroQuantity")]
    [InlineData("negativePortion")]
    public void InvalidRecipesNeverProducePartialConsumption(string variant)
    {
        var (order, recipes, stock) = Create();
        if (variant == "missing") recipes.Clear();
        if (variant == "inactiveRecipe") recipes[0].IsActive = false;
        if (variant == "inactiveIngredient") stock[1].IsActive = false;
        if (variant == "deletedIngredient") stock[1].IsDeleted = true;
        if (variant == "otherRestaurant") recipes[0].RestaurantId = 2;
        if (variant == "wrongUnit") recipes[0].Unit.BaseUnitId = 3;
        if (variant == "zeroRate") recipes[0].Unit.ConversionRateToBase = 0;
        if (variant == "zeroQuantity") order.OrderItems.First().Quantity = 0;
        if (variant == "negativePortion") recipes[0].Quantity = -1;
        var error = Assert.Throws<BusinessRuleException>(() => OrderStockPolicy.BuildPlan(order, recipes, stock));
        Assert.Equal(ErrorCode.KitchenOrderRecipeInvalid, error.Code);
        Assert.Equal(1m, stock[1].QuantityOnHand);
    }

    [Fact]
    public void HidingAnAlreadyOrderedProductDoesNotInvalidateItsActiveRecipe()
    {
        var (order, recipes, stock) = Create();
        recipes[0].Item.IsActive = false;
        Assert.Equal(0.5m, Assert.Single(OrderStockPolicy.BuildPlan(order, recipes, stock)).Quantity);
    }

    [Fact]
    public void TinyConsumptionRoundsUpInsteadOfBecomingFreeAndExactStockIsAllowed()
    {
        var (order, recipes, stock) = Create();
        recipes[0].Quantity = 0.000001m;
        Assert.Equal(0.000001m, Assert.Single(OrderStockPolicy.BuildPlan(order, recipes, stock)).Quantity);
        recipes[0].Quantity = 500;
        Assert.Equal(1m, Assert.Single(OrderStockPolicy.BuildPlan(order, recipes, stock)).Quantity);
    }

    [Fact]
    public void StockChangeNeverGoesNegativeAndRecoveryResetsLowStockNotice()
    {
        var item = new InventoryItem { QuantityOnHand = 1, LowStockThreshold = 0.5m, LastLowStockNotifiedAt = DateTime.UtcNow };
        Assert.Throws<BusinessRuleException>(() => InventoryStockPolicy.ApplyChange(item, -2));
        Assert.Equal(1, item.QuantityOnHand);
        InventoryStockPolicy.ApplyChange(item, 1);
        Assert.Null(item.LastLowStockNotifiedAt);
    }

    /// <summary>İki porsiya üçün qram resepti və kiloqram qalığı qurur; hesablamanın vahid və sifariş sətrinə bağlılığını ölçür.</summary>
    private static (Order, List<Recipe>, Dictionary<int, InventoryItem>) Create()
    {
        var kg = new Unit { Id = 1, Code = "kg", ConversionRateToBase = 1 };
        var g = new Unit { Id = 2, Code = "g", BaseUnitId = 1, ConversionRateToBase = 0.001m };
        var item = new Item { Id = 1, RestaurantId = 1, IsActive = true };
        return (new Order { Id = 1, RestaurantId = 1, OrderItems = [new OrderItem { Id = 7, ItemId = 1, Quantity = 2 }] },
            [new Recipe { ItemId = 1, RestaurantId = 1, InventoryItemId = 1, Quantity = 250, UnitId = 2, Unit = g, Item = item, IsActive = true }],
            new() { [1] = new InventoryItem { Id = 1, RestaurantId = 1, Name = "Flour", IsActive = true, UnitId = 1, Unit = kg, QuantityOnHand = 1 } });
    }
}
