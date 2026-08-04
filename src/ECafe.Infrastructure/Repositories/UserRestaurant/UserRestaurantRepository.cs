using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.UserRestaurant
{
    public class UserRestaurantRepository : BaseRepository<Domain.Entities.UserRestaurant>, IUserRestaurantRepository
    {
        public UserRestaurantRepository(ECafeDbContext context) : base(context)
        {
        }
        public async Task<List<Domain.Entities.UserRestaurant>> GetRestaurantStaffAsync(int restaurantId)
        {
            return await Query()
                .Include(ur => ur.Restaurant)
                .Include(ur => ur.User)
                .ThenInclude(u => u.Role)
                .Include(ur => ur.User)
                .ThenInclude(u => u.File)
                .Where(ur => ur.RestaurantId == restaurantId &&
                ur.IsActive &&
                ur.User.RoleId != (int)RoleCode.Customer)
                .ToListAsync();
        }

        public Task<Domain.Entities.UserRestaurant?> GetActiveByUserIdAsync(int userId)
            => Query(x =>
                    x.UserId == userId &&
                    x.IsActive &&
                    x.Restaurant.IsActive)
                .Include(x => x.Restaurant)
                .FirstOrDefaultAsync();

        public Task<Domain.Entities.UserRestaurant?> GetActiveOwnerByRestaurantAsync(int restaurantId)
            => Query(x =>
                    x.RestaurantId == restaurantId &&
                    x.IsActive &&
                    x.User.IsActive &&
                    x.User.RoleId == (int)RoleCode.Owner)
                .Include(x => x.User)
                .FirstOrDefaultAsync();

        public Task<List<Domain.Entities.UserRestaurant>> GetActiveByRestaurantAndRolesAsync(
            int restaurantId,
            IReadOnlyCollection<int> roleIds)
        {
            return Query(x =>
                    x.RestaurantId == restaurantId &&
                    x.IsActive &&
                    x.User.IsActive &&
                    roleIds.Contains(x.User.RoleId))
                .Include(x => x.User)
                .ToListAsync();
        }
    }
}
