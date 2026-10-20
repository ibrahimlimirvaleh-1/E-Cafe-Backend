using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Category
{
    public interface ICategoryRepository : IBaseRepository<Domain.Entities.Category>
    {
        Task LoadTranslationsAsync(Domain.Entities.Category owner, CancellationToken cancellationToken = default);

        Task<List<Domain.Entities.Category>> GetCategoriesByRestaurantIdAsync(int restaurantId);

        Task<int> GetMaxSortOrderByRestaurantIdAsync(int restaurantId);

        Task<Domain.Entities.Category?> GetByRestaurantAsync(int restaurantId, int categoryId);
    }
}
