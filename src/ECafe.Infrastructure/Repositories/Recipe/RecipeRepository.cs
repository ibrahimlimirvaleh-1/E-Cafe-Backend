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

        public Task<bool> ExistsAsync(int restaurantId, int itemId, int inventoryItemId, int? excludeRecipeId = null)
        {
            var query = Query(r =>
                r.RestaurantId == restaurantId &&
                r.ItemId == itemId &&
                r.InventoryItemId == inventoryItemId);

            if (excludeRecipeId.HasValue)
                query = query.Where(r => r.Id != excludeRecipeId.Value);

            return query.AnyAsync();
        }

        public Task<Domain.Entities.Recipe?> GetByIdForItemAsync(int restaurantId, int itemId, int recipeId)
        {
            return QueryTracked(r =>
                    r.RestaurantId == restaurantId &&
                    r.ItemId == itemId &&
                    r.Id == recipeId)
                .Include(r => r.Item)
                .Include(r => r.InventoryItem)
                .Include(r => r.Unit)
                .FirstOrDefaultAsync();
        }

        public Task<List<Domain.Entities.Recipe>> GetByItemAsync(int restaurantId, int itemId)
        {
            return Query(r =>
                    r.RestaurantId == restaurantId &&
                    r.ItemId == itemId)
                .Include(r => r.Item)
                .Include(r => r.InventoryItem)
                .Include(r => r.Unit)
                .OrderBy(r => r.InventoryItem.Name)
                .ToListAsync();
        }
    }
}
