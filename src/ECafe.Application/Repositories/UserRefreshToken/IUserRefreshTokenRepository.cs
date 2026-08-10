using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserRefreshToken
{
    public interface IUserRefreshTokenRepository : IBaseRepository<Domain.Entities.UserRefreshToken>
    {
        Task<Domain.Entities.UserRefreshToken?> GetByTokenHashTrackedAsync(string tokenHash);
        Task<List<Domain.Entities.UserRefreshToken>> GetActiveByUserIdTrackedAsync(int userId, DateTime nowUtc);
    }
}
