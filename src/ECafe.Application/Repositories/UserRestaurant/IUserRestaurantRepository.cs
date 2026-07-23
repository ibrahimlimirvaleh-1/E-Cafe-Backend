using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserRestaurant
{
    public interface IUserRestaurantRepository : IBaseRepository<Domain.Entities.UserRestaurant>
    {
        public Task<List<Domain.Entities.UserRestaurant>> GetRestaurantStaffAsync(int restaurantId);

        public Task<Domain.Entities.UserRestaurant?> GetActiveOwnerByRestaurantAsync(int restaurantId);
    }
}
