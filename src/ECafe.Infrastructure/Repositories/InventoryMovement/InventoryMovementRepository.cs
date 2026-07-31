using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.InventoryMovement
{
    public class InventoryMovementRepository : BaseRepository<Domain.Entities.InventoryMovement>, IInventoryMovementRepository
    {
        public InventoryMovementRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<List<Domain.Entities.InventoryMovement>> GetByInventoryItemAsync(
            int restaurantId,
            int inventoryItemId,
            CancellationToken cancellationToken = default)
        {
            return Query()
                .Include(x => x.Unit)
                .Include(x => x.MovementType)
                .Where(x => x.RestaurantId == restaurantId && x.InventoryItemId == inventoryItemId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
