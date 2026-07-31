using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Recipe
{
    public interface IRecipeRepository : IBaseRepository<Domain.Entities.Recipe>
    {
        Task<List<Domain.Entities.Recipe>> GetByItemAsync(
            int restaurantId,
            int itemId,
            CancellationToken cancellationToken = default);

        Task<Domain.Entities.Recipe?> GetByRestaurantItemAndInventoryItemAsync(
            int restaurantId,
            int itemId,
            int inventoryItemId,
            CancellationToken cancellationToken = default);
    }
}
