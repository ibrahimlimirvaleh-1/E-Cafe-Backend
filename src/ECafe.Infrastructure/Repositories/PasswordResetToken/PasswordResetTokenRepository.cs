using ECafe.Application.Repositories.PasswordResetToken;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.PasswordResetToken;

public class PasswordResetTokenRepository
    : BaseRepository<Domain.Entities.PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(ECafeDbContext context) : base(context)
    {
    }

    /// <summary>Kiliddən əvvəl yalnız hesab identifikatorunu oxuyur; tokenin istifadə vəziyyətini tracking-də saxlamır.</summary>
    public Task<int?> GetUserIdByTokenHashAsync(string tokenHash)
        => Query(x => x.TokenHash == tokenHash).Select(x => (int?)x.UserId).SingleOrDefaultAsync();

    public Task<Domain.Entities.PasswordResetToken?> GetActiveByTokenHashTrackedAsync(string tokenHash, DateTime nowUtc)
        => QueryTracked(x =>
                x.TokenHash == tokenHash &&
                x.UsedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > nowUtc)
            .Include(x => x.User)
            .FirstOrDefaultAsync();

    public Task<List<Domain.Entities.PasswordResetToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc)
        => QueryTracked(x =>
                x.UserId == userId &&
                x.UsedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > nowUtc)
            .ToListAsync();
}
