using ECafe.Application.Mappings;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class OrderStockReturnPolicyTests
{
    [Theory]
    [InlineData("noRequest")] [InlineData("pending")] [InlineData("used")] [InlineData("legacy")]
    [InlineData("returned")] [InlineData("notCancelled")] [InlineData("deleted")]
    public void UnusedStockCannotBeReturnedWithoutANewKitchenCancellation(string state)
    {
        var (order, _, _) = Valid();
        order.StatusId = 2007;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationRequest = new OrderCancellationRequest { StatusId = OrderCancellationRequestStatus.Approved, IngredientsUnused = true };
        if (state == "noRequest") order.CancellationRequest = null;
        if (state == "pending") order.CancellationRequest!.StatusId = OrderCancellationRequestStatus.PendingKitchen;
        if (state == "used") order.CancellationRequest!.IngredientsUnused = false;
        if (state == "legacy") order.CancellationRequest!.IngredientsUnused = null;
        if (state == "returned") order.CancellationRequest!.StockReturnedAt = DateTime.UtcNow;
        if (state == "notCancelled") order.StatusId = 2003;
        if (state == "deleted") order.IsDeleted = true;
        Assert.Equal(ErrorCode.OrderStockReturnInvalid, Assert.Throws<BusinessRuleException>(() => OrderStockReturnPolicy.EnsureCanRestore(order)).Code);
    }

    [Fact]
    public void ReturnUsesOriginalExactConsumptionRatherThanCurrentOrderQuantity()
    {
        var (order, source, inventory) = Valid();
        order.OrderItems.Single().Quantity = 100;
        OrderStockReturnPolicy.Validate(order, [source], inventory);
        var reversal = OrderStockReturnMovementMapper.Map(source);
        Assert.Equal(0.500001m, reversal.QuantityChange);
        Assert.Equal(source.Id, reversal.ReversedMovementId);
        Assert.Equal(source.UnitId, reversal.UnitId);
        Assert.Equal(source.RelatedOrderItemId, reversal.RelatedOrderItemId);
        Assert.Equal(6, reversal.MovementTypeId);
        Assert.Equal(-0.500001m, source.QuantityChange);
    }

    [Theory]
    [InlineData("missing")] [InlineData("positive")] [InlineData("zero")] [InlineData("precision")]
    [InlineData("restaurant")] [InlineData("order")] [InlineData("line")] [InlineData("lineMissing")]
    [InlineData("type")] [InlineData("reversal")] [InlineData("unit")]
    [InlineData("deletedStock")] [InlineData("missingStock")] [InlineData("foreignStock")]
    [InlineData("negativeStock")] [InlineData("overflow")] [InlineData("duplicate")]
    [InlineData("missingLineConsumption")]
    public void InvalidReturnPlanNeverMutatesInventory(string state)
    {
        var (order, source, inventory) = Valid();
        if (state == "positive") source.QuantityChange = 1;
        if (state == "zero") source.QuantityChange = 0;
        if (state == "precision") source.QuantityChange = -0.0000001m;
        if (state == "restaurant") source.RestaurantId = 2;
        if (state == "order") source.RelatedOrderId = 2;
        if (state == "line") source.RelatedOrderItem!.OrderId = 2;
        if (state == "lineMissing") source.RelatedOrderItem = null;
        if (state == "type") source.MovementTypeId = 6;
        if (state == "reversal") source.ReversedMovementId = 5;
        if (state == "unit") source.UnitId = 2;
        if (state == "deletedStock") inventory[10].IsDeleted = true;
        if (state == "missingStock") inventory.Clear();
        if (state == "foreignStock") inventory[10].RestaurantId = 2;
        if (state == "negativeStock") inventory[10].QuantityOnHand = -1;
        if (state == "overflow") inventory[10].QuantityOnHand = InventoryStockPolicy.MaximumQuantity;
        if (state == "missingLineConsumption") order.OrderItems.Add(new OrderItem { Id = 2, OrderId = 1 });
        var before = inventory.GetValueOrDefault(10)?.QuantityOnHand;
        IReadOnlyList<InventoryMovement> rows = state == "missing" ? [] : state == "duplicate" ? [source, source] : [source];
        Assert.Equal(ErrorCode.OrderStockReturnInvalid, Assert.Throws<BusinessRuleException>(() =>
            OrderStockReturnPolicy.Validate(order, rows, inventory)).Code);
        Assert.Equal(before, inventory.GetValueOrDefault(10)?.QuantityOnHand);
    }

    [Fact]
    public void InactiveIngredientAndSoftDeletedConsumptionKeepTheirOriginalAccounting()
    {
        var (order, source, inventory) = Valid();
        inventory[10].IsActive = false;
        source.IsDeleted = true;
        OrderStockReturnPolicy.Validate(order, [source], inventory);
    }

    /// <summary>Yalnız ilkin uçot və eyni anbar vahidi ilə tam, ayrılmış qaytarma planı yaradır.</summary>
    private static (Order, InventoryMovement, Dictionary<int, InventoryItem>) Valid()
    {
        var line = new OrderItem { Id = 1, OrderId = 1, Quantity = 2 };
        var order = new Order { Id = 1, RestaurantId = 1, OrderItems = [line] };
        var source = new InventoryMovement { Id = 81, RestaurantId = 1, RelatedOrderId = 1, RelatedOrderItemId = 1,
            RelatedOrderItem = line, InventoryItemId = 10, UnitId = 1, QuantityChange = -0.500001m, MovementTypeId = 4 };
        return (order, source, new() { [10] = new InventoryItem { Id = 10, RestaurantId = 1, UnitId = 1, QuantityOnHand = 2, IsActive = true } });
    }
}
