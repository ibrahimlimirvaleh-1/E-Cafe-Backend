using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserPasswordSetupToken
{
    public interface IUserPasswordSetupTokenRepository : IBaseRepository<Domain.Entities.UserPasswordSetupToken>
    {
        Task<Domain.Entities.UserPasswordSetupToken?> GetActiveByTokenHashTrackedAsync(string tokenHash, DateTime nowUtc);

        Task<List<Domain.Entities.UserPasswordSetupToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc);
    }
}
