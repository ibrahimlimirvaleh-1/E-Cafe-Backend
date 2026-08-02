using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Recipe
{
    public interface IRecipeRepository : IBaseRepository<Domain.Entities.Recipe>
    {
        Task<List<Domain.Entities.Recipe>> GetByItemAsync(int restaurantId, int itemId);
        Task<Domain.Entities.Recipe?> GetByIdForItemAsync(int restaurantId, int itemId, int recipeId);
        Task<bool> ExistsAsync(int restaurantId, int itemId, int inventoryItemId, int? excludeRecipeId = null);

    }
}
