using ECafe.Application.Mappings;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed class OrderStockReturnManager(IInventoryItemRepository stock, IInventoryMovementRepository movements) : IOrderStockReturnService
{
    /// <summary>Çağıranın masa transaction-ında stokları sabit ID sırası ilə kilidləyir; ilkin sərfiyyatları eyni vahiddə yalnız bir dəfə əks uçotla qaytarır.</summary>
    public async Task RestoreAsync(Order order, CancellationToken cancellationToken = default)
        => await RestoreCoreAsync(order, null, cancellationToken);

    /// <summary>Ərzaq üzrə qismən qərarı ilkin sərfiyyat mənbələrinə sabit ID sırası ilə bölür; sıfır miqdar stok hərəkəti yaratmır.</summary>
    public Task<bool> RestorePartialAsync(Order order, IReadOnlyList<OrderStockReturnQuantity> quantities, CancellationToken cancellationToken = default)
        => RestoreCoreAsync(order, quantities ?? throw new BusinessRuleException(ErrorCode.OrderStockReturnQuantitiesInvalid), cancellationToken);

    /// <summary>Yalnız ilkin sərfiyyatdan oxunaqlı seçim siyahısı hazırlayır; stok ayırmır, kilid və ya yazı əməliyyatı etmir.</summary>
    public async Task<KitchenStockReturnPreviewResponse> PreviewAsync(Order order, CancellationToken cancellationToken = default)
    {
        var consumption = await ConsumptionAsync(order, cancellationToken);
        var inventory = consumption.GroupBy(row => row.InventoryItemId).ToDictionary(group => group.Key, group => group.First().InventoryItem);
        var zero = inventory.Keys.ToDictionary(id => id, _ => 0m);
        OrderStockReturnPolicy.Validate(order, consumption, inventory, zero);
        return OrderStockReturnPreviewMapper.Map(order.Id, consumption, OrderStockReturnPolicy.ConsumptionTotals(consumption));
    }

    /// <summary>Çağıranın masa transaction-ında stoku kilidləyir, tam planı yoxlayır və anbar, qərar sətirləri, əks uçotu birlikdə hazırlayır.</summary>
    private async Task<bool> RestoreCoreAsync(Order order, IReadOnlyList<OrderStockReturnQuantity>? quantities, CancellationToken cancellationToken)
    {
        OrderStockReturnPolicy.EnsureCanRestore(order);
        var consumption = await ConsumptionAsync(order, cancellationToken);
        var sourceIds = consumption.Select(row => row.Id).ToArray();
        if (await movements.Query().IgnoreQueryFilters().AnyAsync(row =>
            (row.ReversedMovementId.HasValue && sourceIds.Contains(row.ReversedMovementId.Value)) ||
            (row.RelatedOrderId == order.Id && row.MovementTypeId == (int)InventoryMovementTypeCode.StockReturn), cancellationToken))
            throw new BusinessRuleException(ErrorCode.OrderStockReturnInvalid);
        var ids = consumption.Select(row => row.InventoryItemId).Distinct().Order().ToArray();
        await stock.LockForStockChangeAsync(order.RestaurantId, ids, cancellationToken);
        var inventory = await stock.QueryTracked(item => item.RestaurantId == order.RestaurantId && ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var amounts = OrderStockReturnPolicy.ReturnAmounts(consumption, quantities);
        OrderStockReturnPolicy.Validate(order, consumption, inventory, amounts);
        var totals = OrderStockReturnPolicy.ConsumptionTotals(consumption);
        foreach (var pair in amounts.OrderBy(pair => pair.Key))
        {
            if (pair.Value > 0) InventoryStockPolicy.ApplyChange(inventory[pair.Key], pair.Value);
            order.CancellationRequest!.StockReturnLines.Add(OrderCancellationStockReturnLineMapper.Map(inventory[pair.Key], totals[pair.Key], pair.Value));
        }
        foreach (var allocation in OrderStockReturnPolicy.Allocate(consumption, amounts))
            await movements.Add(OrderStockReturnMovementMapper.Map(allocation.Source, allocation.Quantity));
        return amounts.Values.Any(quantity => quantity > 0);
    }

    /// <summary>Silinmiş ilkin sərfiyyatı da uçotdan itirmir; məhsul və vahid məlumatları bir sorğuda oxunur.</summary>
    private Task<List<Domain.Entities.InventoryMovement>> ConsumptionAsync(Order order, CancellationToken token)
        => movements.Query().IgnoreQueryFilters().Where(row => row.RestaurantId == order.RestaurantId && row.RelatedOrderId == order.Id &&
            row.MovementTypeId == (int)InventoryMovementTypeCode.OrderConsumption).Include(row => row.RelatedOrderItem)
            .Include(row => row.InventoryItem).Include(row => row.Unit).OrderBy(row => row.Id).ToListAsync(token);
}
