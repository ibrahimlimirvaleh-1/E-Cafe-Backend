using ECafe.Application.Repositories.Category;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Category
{
    public class CategoryRepository : BaseRepository<Domain.Entities.Category>, ICategoryRepository
    {
        public CategoryRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<List<Domain.Entities.Category>> GetCategoriesByRestaurantIdAsync(int restaurantId)
        {
            return Query()
                .Where(c => c.RestaurantId == restaurantId &&
                                                c.IsActive &&
                                                c.Restaurant.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
        }

        public async Task<int> GetMaxSortOrderByRestaurantIdAsync(int restaurantId)
        {
            return await Query()
                .Where(c => c.RestaurantId == restaurantId && c.IsActive)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync() ?? 0;
        }

        public Task<Domain.Entities.Category?> GetByRestaurantAsync(int restaurantId, int categoryId)
        {
            return QueryTracked(c => c.RestaurantId == restaurantId && c.Id == categoryId)
                .FirstOrDefaultAsync();
        }
    }
}
