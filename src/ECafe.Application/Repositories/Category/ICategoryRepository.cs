using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Category
{
    public interface ICategoryRepository : IBaseRepository<Domain.Entities.Category>
    {
        Task<List<Domain.Entities.Category>> GetCategoriesByRestaurantIdAsync(int restaurantId);

        Task<int> GetMaxSortOrderByRestaurantIdAsync(int restaurantId);
    }
}
