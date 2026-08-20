using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.DTOs.Workflow;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.RestaurantContract.Abstract
{
    public interface IRestaurantContractService
    {
        Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request);

        Task UpdateAsync(int restaurantId, int contractId, UpdateRestaurantContractRequest request);

        Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId);

        Task<PaginatedList<RestaurantContractResponse>> GetPagedByRestaurantAsync(
            int restaurantId,
            RestaurantContractFilterRequest request);

        Task<RestaurantContractResponse> GetActiveAsync(int restaurantId);

        Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(int restaurantId, int contractId);

        Task SendForSignatureAsync(int restaurantId, int contractId);

        Task ApproveAsync(
            int restaurantId,
            int contractId,
            bool hasAcceptedContractTerms,
            string? acceptanceText);

        Task ActivateAsync(int restaurantId, int contractId);

        Task TerminateAsync(int restaurantId, int contractId);

        Task EnsureRestaurantHasActiveContractAsync(int restaurantId);

        Task<int> ExpireActiveContractsAsync(int batchSize);

        Task<int> ActivateDueScheduledContractsAsync(int batchSize);

        Task<int> SendExpiryRemindersAsync(int batchSize);
    }
}
