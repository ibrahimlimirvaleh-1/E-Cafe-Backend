using ECafe.Application.Mappings;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed class OrderStockReturnManager(IInventoryItemRepository stock, IInventoryMovementRepository movements) : IOrderStockReturnService
{
    /// <summary>Çağıranın masa transaction-ında stokları sabit ID sırası ilə kilidləyir; ilkin sərfiyyatları eyni vahiddə yalnız bir dəfə əks uçotla qaytarır.</summary>
    public async Task RestoreAsync(Order order, CancellationToken cancellationToken = default)
    {
        OrderStockReturnPolicy.EnsureCanRestore(order);
        var consumption = await movements.Query().IgnoreQueryFilters()
            .Where(row => row.RestaurantId == order.RestaurantId && row.RelatedOrderId == order.Id &&
                row.MovementTypeId == (int)InventoryMovementTypeCode.OrderConsumption)
            .Include(row => row.RelatedOrderItem).OrderBy(row => row.Id).ToListAsync(cancellationToken);
        var sourceIds = consumption.Select(row => row.Id).ToArray();
        if (await movements.Query().IgnoreQueryFilters().AnyAsync(row =>
            (row.ReversedMovementId.HasValue && sourceIds.Contains(row.ReversedMovementId.Value)) ||
            (row.RelatedOrderId == order.Id && row.MovementTypeId == (int)InventoryMovementTypeCode.StockReturn), cancellationToken))
            throw new BusinessRuleException(ErrorCode.OrderStockReturnInvalid);
        var ids = consumption.Select(row => row.InventoryItemId).Distinct().Order().ToArray();
        await stock.LockForStockChangeAsync(order.RestaurantId, ids, cancellationToken);
        var inventory = await stock.QueryTracked(item => item.RestaurantId == order.RestaurantId && ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        OrderStockReturnPolicy.Validate(order, consumption, inventory);
        foreach (var group in consumption.GroupBy(row => row.InventoryItemId).OrderBy(group => group.Key))
            InventoryStockPolicy.ApplyChange(inventory[group.Key], -group.Sum(row => row.QuantityChange));
        foreach (var row in consumption) await movements.Add(OrderStockReturnMovementMapper.Map(row));
    }
}
