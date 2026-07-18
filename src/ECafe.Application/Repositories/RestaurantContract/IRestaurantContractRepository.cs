using ECafe.Application.Repository;
using ECafe.Domain.Entities;

namespace ECafe.Application.Repositories.RestaurantContract
{
    public interface IRestaurantContractRepository : IBaseRepository<Domain.Entities.RestaurantContract>
    {
        Task<List<Domain.Entities.RestaurantContract>> GetByRestaurantAsync(int restaurantId);

        Task<Domain.Entities.RestaurantContract?> GetActiveByRestaurantAsync(int restaurantId);

        Task<Domain.Entities.RestaurantContract?> GetTrackedByRestaurantAsync(int restaurantId, int contractId);

        Task<bool> HasActiveContractAsync(int restaurantId);
    }
}
