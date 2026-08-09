using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.LoginAttempt;

public interface ILoginAttemptRepository : IBaseRepository<Domain.Entities.LoginAttempt>
{
    Task<int> CountFailedAttemptsAsync(string email, DateTime fromUtc);
    Task<DateTime?> GetActiveLockoutUntilAsync(string email, DateTime nowUtc);
}
