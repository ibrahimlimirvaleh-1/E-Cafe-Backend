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
