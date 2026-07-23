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


        public async Task<Domain.Entities.User?> GetByEmailAsync(string email)
        {
            return await Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurant!)
                .ThenInclude(ur => ur.Restaurant)
                .Where(u => u.Email == email).FirstOrDefaultAsync();
        }

        public Task<Domain.Entities.User?> GetByEmailTrackedAsync(string email)
        {
            return UserWithAuthDetailsTrackedQuery()
                .FirstOrDefaultAsync(u => u.Email == email);
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

        public Task<List<Domain.Entities.User>> GetActiveUsersByRoleAsync(int roleId)
            => Query(x => x.RoleId == roleId && x.IsActive)
                .ToListAsync();

        public IQueryable<Domain.Entities.User> GetUsersForList(int? restaurantId)
        {
            var query = Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurant)
                .AsQueryable();

            if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                query = query.Where(u =>
                    u.UserRestaurant != null &&
                    u.UserRestaurant.RestaurantId == restaurantId.Value &&
                    u.UserRestaurant.IsActive);
            }


            return query;
        }

        public Task<Domain.Entities.User?> GetProfileByIdAsync(int userId)
        {
            return UserWithDetailsQuery()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public Task<Domain.Entities.User?> GetProfileByIdTrackedAsync(int userId)
        {
            return QueryTracked()
                .Include(u => u.File)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public Task<Domain.Entities.User?> GetStaffDetailAsync(int restaurantId, int staffId)
        {
            return UserWithDetailsQuery()
                .FirstOrDefaultAsync(u =>
                    u.Id == staffId &&
                    u.UserRestaurant != null &&
                    u.UserRestaurant.RestaurantId == restaurantId &&
                    u.UserRestaurant.IsActive);
        }

        public Task<Domain.Entities.User?> GetProfileConflictAsync(int userId, string email, string phone)
        {
            return Query()
                .FirstOrDefaultAsync(u =>
                    u.Id != userId &&
                    (u.Email == email || u.Phone == phone));
        }

        private IQueryable<Domain.Entities.User> UserWithDetailsQuery()
        {
            return Query()
                .Include(u => u.Role)
                .Include(u => u.File)
                .Include(u => u.UserRestaurant);
        }

        private IQueryable<Domain.Entities.User> UserWithAuthDetailsTrackedQuery()
        {
            return QueryTracked()
                .Include(u => u.Role)
                .Include(u => u.File)
                .Include(u => u.UserRestaurant!)
                .ThenInclude(ur => ur.Restaurant);
        }
    }
}
