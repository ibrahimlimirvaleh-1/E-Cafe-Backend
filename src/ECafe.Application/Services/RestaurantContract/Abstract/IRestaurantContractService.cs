using ECafe.Application.DTOs.RestaurantContract;

namespace ECafe.Application.Services.RestaurantContract.Abstract
{
    public interface IRestaurantContractService
    {
        Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request);

        Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId);

        Task<RestaurantContractResponse> GetActiveAsync(int restaurantId);

        Task ActivateAsync(int restaurantId, int contractId, int? signedByUserId);

        Task TerminateAsync(int restaurantId, int contractId);

        Task EnsureRestaurantHasActiveContractAsync(int restaurantId);
    }
}
