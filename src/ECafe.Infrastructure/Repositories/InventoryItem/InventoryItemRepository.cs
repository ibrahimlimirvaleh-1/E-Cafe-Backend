using ECafe.Application.Repositories.InventoryItem;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.InventoryItem
{
    public class InventoryItemRepository : BaseRepository<Domain.Entities.InventoryItem>, IInventoryItemRepository
    {
        public InventoryItemRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<Domain.Entities.InventoryItem?> GetInventoryByRestaurantIdAsync(int id, int restaurantId)
            => QueryTracked().FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == restaurantId);

   
        
    }
}
