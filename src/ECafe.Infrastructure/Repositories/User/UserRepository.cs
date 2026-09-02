using ECafe.Application.Repositories.User;
using ECafe.Domain.Enums;
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
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Include(u => u.UserRestaurants)
                .ThenInclude(ur => ur.Restaurant)
                .Where(u => u.Email == email).FirstOrDefaultAsync();
        }

        public Task<Domain.Entities.User?> GetByEmailTrackedAsync(string email)
        {
            return UserWithAuthDetailsTrackedQuery()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public Task<Domain.Entities.User?> GetByIdWithAuthDetailsTrackedAsync(int userId)
        {
            return UserWithAuthDetailsTrackedQuery()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<List<Domain.Entities.User>> GetByRestaurantIdAsync(int restaurantId)
        {
            return await Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurants)
                .Where(u => u.UserRestaurants.Any(ur =>
                            ur.RestaurantId == restaurantId &&
                            ur.IsActive))
                .ToListAsync();
        }

        public Task<List<Domain.Entities.User>> GetActiveUsersByRoleAsync(int roleId)
            => Query(x => x.RoleId == roleId && x.IsActive)
                .ToListAsync();

        public IQueryable<Domain.Entities.User> GetUsersForList(int? restaurantId)
        {
            var query = Query()
                .Include(u => u.Role)
                .Include(u => u.UserRestaurants)
                .AsQueryable();

            if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                query = query.Where(u =>
                    u.UserRestaurants.Any(ur =>
                        ur.RestaurantId == restaurantId.Value &&
                        ur.IsActive));
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

        public Task<bool> IsActiveAsync(int userId)
            => Query()
                .AnyAsync(u => u.Id == userId && u.IsActive);

        public async Task<(bool IsActive, int SessionVersion)?> GetSessionStateAsync(int userId)
        {
            var state = await Query()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.IsActive,
                    u.SessionVersion
                })
                .FirstOrDefaultAsync();

            return state is null
                ? null
                : (state.IsActive, state.SessionVersion);
        }

        public Task<Domain.Entities.User?> GetStaffDetailAsync(int restaurantId, int staffId)
        {
            return UserWithDetailsQuery()
                .FirstOrDefaultAsync(u =>
                    u.Id == staffId &&
                    u.UserRestaurants.Any(ur =>
                        ur.RestaurantId == restaurantId &&
                        ur.IsActive));
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
                .Include(u => u.UserRestaurants)
                .ThenInclude(ur => ur.Restaurant);
        }

        private IQueryable<Domain.Entities.User> UserWithAuthDetailsTrackedQuery()
        {
            return QueryTracked()
                .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Include(u => u.File)
                .Include(u => u.UserRestaurants)
                .ThenInclude(ur => ur.Restaurant);
        }

        public Task<Domain.Entities.User?> GetOwnerByEmailAsync(string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            return Query()
                .FirstOrDefaultAsync(u =>
                    u.RoleId == (int)RoleCode.Owner &&
                    u.Email == normalizedEmail);
        }
    }
}
