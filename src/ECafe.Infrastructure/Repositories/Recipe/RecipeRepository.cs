using ECafe.Application.Repositories.Recipe;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Recipe
{
    public class RecipeRepository : BaseRepository<Domain.Entities.Recipe>, IRecipeRepository
    {
        public RecipeRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<List<Domain.Entities.Recipe>> GetByItemAsync(
            int restaurantId,
            int itemId,
            CancellationToken cancellationToken = default)
        {
            return Query()
                .Include(x => x.InventoryItem)
                .Include(x => x.Unit)
                .Where(x => x.RestaurantId == restaurantId && x.ItemId == itemId)
                .OrderBy(x => x.InventoryItem.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<Domain.Entities.Recipe?> GetByRestaurantItemAndInventoryItemAsync(
            int restaurantId,
            int itemId,
            int inventoryItemId,
            CancellationToken cancellationToken = default)
        {
            return QueryTracked()
                .FirstOrDefaultAsync(
                    x => x.RestaurantId == restaurantId &&
                         x.ItemId == itemId &&
                         x.InventoryItemId == inventoryItemId,
                    cancellationToken);
        }
    }
}
