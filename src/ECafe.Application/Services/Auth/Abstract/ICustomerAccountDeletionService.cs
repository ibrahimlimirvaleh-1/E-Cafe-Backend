using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract;

public interface ICustomerAccountDeletionService
{
    Task<CustomerAccountDeletionEligibility> GetEligibilityAsync(CancellationToken cancellationToken);
    Task DeleteAsync(CustomerAccountDeletionRequest request, CancellationToken cancellationToken);
}
