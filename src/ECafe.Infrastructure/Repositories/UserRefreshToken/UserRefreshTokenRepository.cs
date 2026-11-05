using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.UserRefreshToken
{
    public class UserRefreshTokenRepository
        : BaseRepository<Domain.Entities.UserRefreshToken>, IUserRefreshTokenRepository
    {
        public UserRefreshTokenRepository(ECafeDbContext context) : base(context)
        {
        }

        /// <summary>Tokenin sahibini tracking-siz müəyyən edir ki, kiliddən əvvəl köhnə token vəziyyəti kontekstdə qalmasın.</summary>
        public Task<int?> GetUserIdByTokenHashAsync(string tokenHash)
            => Query(t => t.TokenHash == tokenHash).Select(t => (int?)t.UserId).SingleOrDefaultAsync();

        /// <summary>Refresh və native çıxışı eyni istifadəçi sətrinin transaction kilidi ilə ardıcıllaşdırır; schema dəyişmir.</summary>
        public Task AcquireUserLockAsync(int userId)
            => Context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM auth.users WHERE id = {userId} FOR UPDATE");

        public Task<Domain.Entities.UserRefreshToken?> GetByTokenHashTrackedAsync(string tokenHash)
        {
            return QueryTracked()
                .Include(t => t.User)
                    .ThenInclude(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .Include(t => t.User)
                    .ThenInclude(u => u.File)
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRestaurants)
                    .ThenInclude(ur => ur.Restaurant)
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRestaurants)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public Task<List<Domain.Entities.UserRefreshToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc)
        {
            return QueryTracked(t =>
                    t.UserId == userId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > nowUtc)
                .ToListAsync();
        }

        public Task<List<Domain.Entities.UserRefreshToken>> GetActiveByUserIdAsync(int userId, DateTime nowUtc)
        {
            return Query(t =>
                    t.UserId == userId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > nowUtc &&
                    t.SessionId != string.Empty)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public Task<List<Domain.Entities.UserRefreshToken>> GetActiveByUserSessionTrackedAsync(int userId, string sessionId, DateTime nowUtc)
        {
            return QueryTracked(t =>
                    t.UserId == userId &&
                    t.SessionId == sessionId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > nowUtc)
                .ToListAsync();
        }

        public Task<bool> HasActiveUserSessionAsync(int userId, string sessionId, DateTime nowUtc)
        {
            return Query(t =>
                    t.UserId == userId &&
                    t.SessionId == sessionId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > nowUtc)
                .AnyAsync();
        }
    }
}
