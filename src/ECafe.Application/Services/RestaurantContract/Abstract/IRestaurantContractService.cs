using ECafe.Application.DTOs.RestaurantContract;

namespace ECafe.Application.Services.RestaurantContract.Abstract
{
    public interface IRestaurantContractService
    {
        Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request);

        Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId);

        Task<RestaurantContractResponse> GetActiveAsync(int restaurantId);

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
    }
}
