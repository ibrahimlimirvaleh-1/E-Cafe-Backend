using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.InventoryItem
{
    public interface IInventoryItemRepository : IBaseRepository<Domain.Entities.InventoryItem>
    {
        Task<Domain.Entities.InventoryItem?> GetInventoryByRestaurantIdAsync(int id, int restaurantId);
    }
}
