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

        /// <summary>Yoxlanmış izlənən kateqoriyanın deaktiv dillər daxil bütün tərcümələrini dəyişiklik üçün yükləyir.</summary>
        public Task LoadTranslationsAsync(Domain.Entities.Category owner, CancellationToken cancellationToken = default)
            => Context.Entry(owner).Collection(entity => entity.Translations).Query()
                .IgnoreQueryFilters().LoadAsync(cancellationToken);

        public Task<List<Domain.Entities.Category>> GetCategoriesByRestaurantIdAsync(int restaurantId)
        {
            return Query()
                .Where(c => c.RestaurantId == restaurantId &&
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
