using ECafe.Application.Mappings;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Repositories.Recipe;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed class OrderStockManager(IRecipeRepository recipes, IInventoryItemRepository stock,
    IInventoryMovementRepository movements, IInventoryLowStockNotifier lowStock) : IOrderStockService
{
    /// <summary>Çağıranın sifariş transaction-ında bütün inqrediyentləri ID sırası ilə kilidləyir; əvvəl hamısını yoxlayır, sonra qalığı və uçotu birlikdə dəyişir.</summary>
    public async Task ConsumeAsync(Order order, CancellationToken cancellationToken)
    {
        if (await movements.Query().IgnoreQueryFilters().AnyAsync(movement => movement.RestaurantId == order.RestaurantId &&
            movement.RelatedOrderId == order.Id && movement.MovementTypeId == (int)InventoryMovementTypeCode.OrderConsumption,
            cancellationToken)) throw new BusinessRuleException(ErrorCode.KitchenOrderStockStateInvalid);
        var ids = order.OrderItems.Where(line => !line.IsDeleted).Select(line => line.ItemId).Distinct().ToArray();
        var rows = await recipes.Query(recipe => recipe.RestaurantId == order.RestaurantId && ids.Contains(recipe.ItemId) && recipe.IsActive)
            .AsNoTracking().Include(recipe => recipe.Unit).Include(recipe => recipe.Item).ToListAsync(cancellationToken);
        var ingredientIds = rows.Select(recipe => recipe.InventoryItemId).Distinct().Order().ToArray();
        await stock.LockForStockChangeAsync(order.RestaurantId, ingredientIds, cancellationToken);
        var inventory = await stock.QueryTracked(item => item.RestaurantId == order.RestaurantId && ingredientIds.Contains(item.Id))
            .Include(item => item.Unit).ToDictionaryAsync(item => item.Id, cancellationToken);
        var plan = OrderStockPolicy.BuildPlan(order, rows, inventory);
        var costByLine = order.OrderItems.Where(line => !line.IsDeleted).ToDictionary(line => line.Id, _ => (decimal?)0m);
        foreach (var group in plan.GroupBy(row => row.InventoryItemId).OrderBy(group => group.Key))
        {
            var item = inventory[group.Key];
            var before = item.QuantityOnHand;
            foreach (var row in group)
            {
                var cost = InventoryCostPolicy.Remove(item, row.Quantity);
                costByLine[row.OrderItemId] += cost;
                await movements.Add(OrderStockMovementMapper.Map(order, row, cost));
            }
            await lowStock.NotifyAsync(item, item.Unit, before, cancellationToken);
        }
        foreach (var line in order.OrderItems.Where(line => !line.IsDeleted))
            line.IngredientCostSnapshot = costByLine[line.Id];
        // Status, audit, bildiriş və stok vahid DbContext ilə çağıran xidmətdə saxlanılır və commit edilir.
    }
}
