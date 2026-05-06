using ECafe.Application.Repositories.User;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.User
{
    public class UserRepository : BaseRepository<Domain.Entities.User>, IUserRepository
    {
        public UserRepository(ECafeDbContext context) : base(context)
        {
        }


        public async Task<Domain.Entities.User> GetByEmailAsync(string email)
        {
            return await Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurant)
                .ThenInclude(ur => ur.Restaurant)
                .Where(u => u.Email == email).FirstOrDefaultAsync();
        }

        public async Task<List<Domain.Entities.User>> GetByRestaurantIdAsync(int restaurantId)
        {
            return await Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurant)
                .Where(u => u.UserRestaurant != null &&
                            u.UserRestaurant.RestaurantId == restaurantId &&
                            u.UserRestaurant.IsActive)
                .ToListAsync();
        }
    }
}
