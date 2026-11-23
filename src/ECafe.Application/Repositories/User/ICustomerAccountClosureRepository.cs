using ECafe.Domain.Enums;

namespace ECafe.Application.Repositories.User;

public interface ICustomerAccountClosureRepository
{
    Task<IReadOnlyList<CustomerAccountClosureBlocker>> GetBlockersAsync(int userId, CancellationToken cancellationToken);
    Task RevokeRecoveryAndPushAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken);
}
