using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.PasswordResetToken;

public interface IPasswordResetTokenRepository : IBaseRepository<Domain.Entities.PasswordResetToken>
{
    Task<Domain.Entities.PasswordResetToken?> GetActiveByTokenHashTrackedAsync(string tokenHash, DateTime nowUtc);

    Task<List<Domain.Entities.PasswordResetToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc);
}
