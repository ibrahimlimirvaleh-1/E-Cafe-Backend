using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class InventoryCostPolicyTests
{
    /// <summary>Litr və kiloqram alışlarını millilitr və qram resepti ilə tutuşdurur; yarım qəpik itmir.</summary>
    [Fact]
    public void AmericanoKeepsSubCentRecipeCost()
    {
        var litre = new Unit { Id = 1, ConversionRateToBase = 1m };
        var millilitre = new Unit { Id = 2, BaseUnitId = 1, ConversionRateToBase = 0.001m };
        var kilogram = new Unit { Id = 3, ConversionRateToBase = 1m };
        var gram = new Unit { Id = 4, BaseUnitId = 3, ConversionRateToBase = 0.001m };
        var water = new InventoryItem();
        var coffee = new InventoryItem();
        InventoryCostPolicy.Add(water, 1, InventoryCostPolicy.PurchaseAmount(1, 0.50m));
        InventoryCostPolicy.Add(coffee, 1, InventoryCostPolicy.PurchaseAmount(1, 5m));

        var waterCost = InventoryCostPolicy.Remove(water, InventoryStockPolicy.ConvertQuantity(50, millilitre, litre));
        var coffeeCost = InventoryCostPolicy.Remove(coffee, InventoryStockPolicy.ConvertQuantity(20, gram, kilogram));
        Assert.Equal(0.025m, waterCost);
        Assert.Equal(0.1m, coffeeCost);
        Assert.Equal(0.125m, waterCost + coffeeCost);
        Assert.Equal(0.475m, water.StockCostValue);
        Assert.Equal(4.9m, coffee.StockCostValue);
    }

    /// <summary>Bir neçə alışdan orta maya, qismən qaytarışdan isə orijinal sərfiyyatın mütənasib dəyəri istifadə olunur.</summary>
    [Fact]
    public void ValuationIsWeightedAndReturnsUseHistoricalCost()
    {
        var item = new InventoryItem();
        InventoryCostPolicy.Add(item, 2, InventoryCostPolicy.PurchaseAmount(2, 4));
        InventoryCostPolicy.Add(item, 1, InventoryCostPolicy.PurchaseAmount(1, 10));
        var used = InventoryCostPolicy.Remove(item, 1);
        Assert.Equal(6m, used);
        InventoryCostPolicy.Add(item, 1, InventoryCostPolicy.PurchaseAmount(1, 12));
        var returned = InventoryCostPolicy.Allocate(used, 0.25m, 1m);
        InventoryCostPolicy.Add(item, 0.25m, returned);
        Assert.Equal(25.5m, item.StockCostValue);
        Assert.Equal(3.25m, item.QuantityOnHand);
        Assert.Equal(25.5m, InventoryCostPolicy.Remove(item, 3.25m));
        Assert.Null(item.StockCostValue);
    }

    /// <summary>Naməlum qalıq sıfır qiymətli stok sayılmır, amma ayrıca pulsuz alışın sıfır mayası etibarlıdır.</summary>
    [Fact]
    public void UnpricedOpeningStockDoesNotProduceInventedProfit()
    {
        var item = new InventoryItem { QuantityOnHand = 1 };
        InventoryCostPolicy.Add(item, 1, InventoryCostPolicy.PurchaseAmount(1, 5));
        Assert.Null(InventoryCostPolicy.Remove(item, 2));
        InventoryCostPolicy.Add(item, 1, InventoryCostPolicy.PurchaseAmount(1, 0));
        Assert.Equal(0m, InventoryCostPolicy.Remove(item, 1));
        Assert.Null(InventoryCostPolicy.PurchaseAmount(1, null));
    }

    /// <summary>Mənfi, çox dəqiq və yuxarı həddi aşan alış qiymətlərini rədd edir.</summary>
    [Theory]
    [InlineData("-0.01")]
    [InlineData("0.0000001")]
    [InlineData("1000000.01")]
    public void InvalidPriceIsRejected(string input)
    {
        var price = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        var error = Assert.Throws<BusinessRuleException>(() => InventoryCostPolicy.PurchaseAmount(1, price));
        Assert.Equal(ErrorCode.InventoryPurchasePriceInvalid, error.Code);
    }
}
