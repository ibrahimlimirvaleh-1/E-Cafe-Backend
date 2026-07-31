using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.InventoryMovement
{
    public interface IInventoryMovementRepository : IBaseRepository<Domain.Entities.InventoryMovement>
    {
        Task<List<Domain.Entities.InventoryMovement>> GetByInventoryItemAsync(
            int restaurantId,
            int inventoryItemId,
            CancellationToken cancellationToken = default);
    }
}
