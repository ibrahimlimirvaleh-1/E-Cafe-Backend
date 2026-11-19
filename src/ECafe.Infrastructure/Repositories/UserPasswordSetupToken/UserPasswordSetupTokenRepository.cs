using ECafe.Application.Repositories.UserPasswordSetupToken;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.UserPasswordSetupToken
{
    public class UserPasswordSetupTokenRepository
        : BaseRepository<Domain.Entities.UserPasswordSetupToken>, IUserPasswordSetupTokenRepository
    {
        public UserPasswordSetupTokenRepository(ECafeDbContext context) : base(context)
        {
        }

        /// <summary>İlkin parol linkinin sahibini kiliddən əvvəl tracking-siz tapır; aktivliyi kiliddən sonra yenidən yoxlanılır.</summary>
        public Task<int?> GetUserIdByTokenHashAsync(string tokenHash)
            => Query(token => token.TokenHash == tokenHash).Select(token => (int?)token.UserId).SingleOrDefaultAsync();

        public Task<Domain.Entities.UserPasswordSetupToken?> GetActiveByTokenHashTrackedAsync(string tokenHash, DateTime nowUtc)
            => QueryTracked(x =>
                    x.TokenHash == tokenHash &&
                    x.UsedAt == null &&
                    x.ExpiresAt > nowUtc)
                .Include(x => x.User)
                .FirstOrDefaultAsync();

        public Task<List<Domain.Entities.UserPasswordSetupToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc)
            => QueryTracked(x =>
                    x.UserId == userId &&
                    x.UsedAt == null &&
                    x.ExpiresAt > nowUtc)
                .ToListAsync();
    }
}
