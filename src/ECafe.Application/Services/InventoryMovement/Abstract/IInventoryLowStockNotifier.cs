using ECafe.Domain.Entities;

namespace ECafe.Application.Services.InventoryMovement.Abstract;

public interface IInventoryLowStockNotifier
{
    Task NotifyAsync(Domain.Entities.InventoryItem item, Unit unit, decimal before, CancellationToken cancellationToken = default);
}
