using ECafe.Application.Repositories.Restaurant;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Restaurant
{
    public class RestaurantRepository : BaseRepository<Domain.Entities.Restaurant>,IRestaurantRepository
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
    }
}
