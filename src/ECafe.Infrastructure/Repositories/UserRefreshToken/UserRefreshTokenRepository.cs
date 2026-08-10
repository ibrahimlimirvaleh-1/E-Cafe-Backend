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

        public Task<Domain.Entities.UserRefreshToken?> GetByTokenHashTrackedAsync(string tokenHash)
        {
            return QueryTracked()
                .Include(t => t.User)
                    .ThenInclude(u => u.Role)
                .Include(t => t.User)
                    .ThenInclude(u => u.File)
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRestaurant)
                    .ThenInclude(ur => ur!.Restaurant)
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
    }
}
