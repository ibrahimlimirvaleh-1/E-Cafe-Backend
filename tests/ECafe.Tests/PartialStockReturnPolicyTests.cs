using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.ValueObjects;
using Xunit;

namespace ECafe.Tests;

public sealed class PartialStockReturnPolicyTests
{
    [Fact]
    public void WireContractPreservesAllEighteenDecimalDigitsForBrowsers()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var body = System.Text.Json.JsonSerializer.Deserialize<ECafe.Application.DTOs.Order.DecideOrderCancellationRequest>(
            """{"approved":true,"reason":"Mətbəx qərarı","stockReturnMode":3,"stockReturns":[{"inventoryItemId":1,"returnQuantity":"999999999999.123456"}]}""", options)!;
        Assert.Equal(999999999999.123456m, Assert.Single(body.StockReturns!).ReturnQuantity);
        var response = new ECafe.Application.DTOs.Order.OrderStockReturnQuantityResponse(1, 999999999999.123456m);
        Assert.Equal("999999999999.123456", response.ReturnQuantityText);
    }

    [Theory]
    [InlineData(null, OrderStockReturnMode.Used)]
    [InlineData(false, OrderStockReturnMode.Used)]
    [InlineData(true, OrderStockReturnMode.Unused)]
    public void LegacyBooleanRetainsItsOriginalMeaning(bool? unused, OrderStockReturnMode expected)
        => Assert.Equal(expected, OrderStockReturnDecisionPolicy.Normalize(true, unused, null, null).Mode);

    [Theory]
    [InlineData("missing")] [InlineData("empty")] [InlineData("duplicate")] [InlineData("negative")]
    [InlineData("precision")] [InlineData("zeroId")] [InlineData("overflow")]
    [InlineData("unknownMode")] [InlineData("mixedContract")] [InlineData("rejectWithPlan")]
    [InlineData("fullWithLines")] [InlineData("nullLine")]
    public void InvalidContractsAreRejectedBeforeMutatingAnOrder(string state)
    {
        IReadOnlyList<OrderStockReturnQuantity>? quantities = [new(1, 0.2m)];
        var mode = OrderStockReturnMode.ByIngredient;
        bool? legacy = null;
        if (state == "missing") quantities = null;
        if (state == "empty") quantities = [];
        if (state == "duplicate") quantities = [new(1, 0), new(1, 0.1m)];
        if (state == "negative") quantities = [new(1, -0.1m)];
        if (state == "precision") quantities = [new(1, 0.0000001m)];
        if (state == "zeroId") quantities = [new(0, 1)];
        if (state == "overflow") quantities = [new(1, InventoryStockPolicy.MaximumQuantity + 1)];
        if (state == "unknownMode") mode = (OrderStockReturnMode)99;
        if (state == "mixedContract") legacy = false;
        if (state == "fullWithLines") mode = OrderStockReturnMode.Unused;
        if (state == "nullLine") quantities = [null!];
        Assert.Equal(ErrorCode.OrderStockReturnQuantitiesInvalid, Assert.Throws<BusinessRuleException>(() =>
            OrderStockReturnDecisionPolicy.Normalize(state != "rejectWithPlan", legacy, mode, quantities)).Code);
    }

    [Fact]
    public void SortedCompletePlanIsImmutableAfterItsFirstDecision()
    {
        var request = new OrderCancellationRequest { StatusId = OrderCancellationRequestStatus.Approved,
            ResolvedAt = DateTime.UtcNow, StockReturnMode = OrderStockReturnMode.ByIngredient,
            StockReturnLines = [new() { InventoryItemId = 1, ConsumedQuantity = 1, ReturnQuantity = 0.2m },
                new() { InventoryItemId = 2, ConsumedQuantity = 1, ReturnQuantity = 0 }] };
        var same = OrderStockReturnDecisionPolicy.Normalize(true, null, OrderStockReturnMode.ByIngredient, [new(2, 0), new(1, 0.20m)]);
        OrderStockReturnDecisionPolicy.EnsureReplayMatches(request, same);
        Assert.Equal(ErrorCode.OrderCancellationRequestConflict, Assert.Throws<BusinessRuleException>(() =>
            OrderStockReturnDecisionPolicy.EnsureReplayMatches(request, same with { Returns = [new(1, 0.3m), new(2, 0)] })).Code);
        Assert.Throws<BusinessRuleException>(() => OrderStockReturnDecisionPolicy.EnsureReplayMatches(request,
            new(OrderStockReturnMode.Used, [])));
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("exceeds")]
    public void EveryOriginalIngredientMustBePresentWithinItsConsumption(string state)
    {
        var source = new InventoryMovement { InventoryItemId = 1, QuantityChange = -0.5m };
        OrderStockReturnQuantity[] quantities = state == "missing" ? [] : state == "extra" ? [new(2, 0.1m)] : [new(1, 0.500001m)];
        Assert.Equal(ErrorCode.OrderStockReturnQuantitiesInvalid, Assert.Throws<BusinessRuleException>(() =>
            OrderStockReturnPolicy.ReturnAmounts([source], quantities)).Code);
    }

    [Fact]
    public void IngredientTotalsComeFromLedgerRatherThanCurrentRecipeOrOrderQuantity()
    {
        var sources = new[] { new InventoryMovement { InventoryItemId = 1, QuantityChange = -0.2m },
            new InventoryMovement { InventoryItemId = 1, QuantityChange = -0.3m },
            new InventoryMovement { InventoryItemId = 2, QuantityChange = -0.4m } };
        var totals = OrderStockReturnPolicy.ConsumptionTotals(sources);
        Assert.Equal(0.5m, totals[1]);
        var amounts = OrderStockReturnPolicy.ReturnAmounts(sources, [new(1, 0.123456m), new(2, 0)]);
        Assert.Equal(0.123456m, amounts[1]);
        Assert.Equal(0m, amounts[2]);
    }

    [Fact]
    public void PartialReturnChecksActualReturnedAmountRatherThanAssumingAFullReversal()
    {
        var item = new InventoryItem { Id = 1, RestaurantId = 1, UnitId = 1, QuantityOnHand = InventoryStockPolicy.MaximumQuantity - 0.1m };
        var line = new OrderItem { Id = 1, OrderId = 1, Quantity = 1 };
        var order = new Order { Id = 1, RestaurantId = 1, OrderItems = [line] };
        var source = new InventoryMovement { Id = 1, RestaurantId = 1, InventoryItemId = 1, UnitId = 1,
            RelatedOrderId = 1, RelatedOrderItemId = 1, RelatedOrderItem = line, MovementTypeId = 4, QuantityChange = -0.5m };
        OrderStockReturnPolicy.Validate(order, [source], new Dictionary<int, InventoryItem> { [1] = item },
            new Dictionary<int, decimal> { [1] = 0.1m });
        Assert.Equal(ErrorCode.OrderStockReturnInvalid, Assert.Throws<BusinessRuleException>(() =>
            OrderStockReturnPolicy.Validate(order, [source], new Dictionary<int, InventoryItem> { [1] = item })).Code);
    }

    [Fact]
    public void AllocationIsStableAndNeverRoundsOrChangesOriginalMovements()
    {
        var first = new InventoryMovement { Id = 10, InventoryItemId = 1, QuantityChange = -0.2m };
        var second = new InventoryMovement { Id = 5, InventoryItemId = 1, QuantityChange = -0.3m };
        var plan = OrderStockReturnPolicy.Allocate([first, second], new Dictionary<int, decimal> { [1] = 0.400001m });
        Assert.Equal(5, plan[0].Source.Id);
        Assert.Equal(0.3m, plan[0].Quantity);
        Assert.Equal(0.100001m, plan[1].Quantity);
        Assert.Equal(-0.2m, first.QuantityChange);
    }
}
