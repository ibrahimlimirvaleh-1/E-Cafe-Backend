using ECafe.Application.Repositories.RestaurantGroup;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.RestaurantGroup
{
    public class RestaurantGroupRepository
        : BaseRepository<Domain.Entities.RestaurantGroup>, IRestaurantGroupRepository
    {
        public RestaurantGroupRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
