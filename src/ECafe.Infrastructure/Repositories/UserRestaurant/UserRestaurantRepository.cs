using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.UserRestaurant
{
    public class UserRestaurantRepository : BaseRepository<Domain.Entities.UserRestaurant>, IUserRestaurantRepository
    {
        public UserRestaurantRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
