using ECafe.Application.Repositories.LoginAttempt;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.LoginAttempt;

public class LoginAttemptRepository : BaseRepository<Domain.Entities.LoginAttempt>, ILoginAttemptRepository
{
    public LoginAttemptRepository(ECafeDbContext context) : base(context)
    {
    }

    public Task<int> CountFailedAttemptsAsync(string email, DateTime fromUtc)
        => Query(x =>
                x.Email == email &&
                !x.IsSuccessful &&
                x.AttemptedAt >= fromUtc)
            .CountAsync();

    public Task<DateTime?> GetActiveLockoutUntilAsync(string email, DateTime nowUtc)
        => Query(x =>
                x.Email == email &&
                x.LockoutUntil != null &&
                x.LockoutUntil > nowUtc)
            .MaxAsync(x => x.LockoutUntil);
}
