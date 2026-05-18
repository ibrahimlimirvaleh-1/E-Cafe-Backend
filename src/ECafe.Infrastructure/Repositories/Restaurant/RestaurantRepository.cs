using ECafe.Application.Repositories.Restaurant;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Restaurant
{
    public class RestaurantRepository : BaseRepository<Domain.Entities.Restaurant>, IRestaurantRepository
    {
        public RestaurantRepository(ECafeDbContext context) : base(context)
        {
        }

        public IQueryable<Domain.Entities.Restaurant> GetActiveRestaurants()
        {
            return Query()
                .Include(r => r.Files)
                .Where(r => r.IsActive);
        }

        public Task<Domain.Entities.Restaurant?> GetRestaurantInfoAsync(int id)
        {
            return Query()
                .Include(r => r.Files)
                .Include(r => r.Tables)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive && r.Tables.Any(t => t.IsActive));
        }
    }
}
