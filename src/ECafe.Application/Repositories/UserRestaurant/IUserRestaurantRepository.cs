using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserRestaurant
{
    public interface IUserRestaurantRepository : IBaseRepository<Domain.Entities.UserRestaurant>
    {
        public Task<List<Domain.Entities.UserRestaurant>> GetRestaurantStaffAsync(int restaurantId);

        public Task<Domain.Entities.UserRestaurant?> GetActiveByUserIdAsync(int userId);

        public Task<Domain.Entities.UserRestaurant?> GetActiveStaffAssignmentAsync(int restaurantId, int staffId);

        public Task<Domain.Entities.UserRestaurant?> GetStaffAssignmentAsync(int restaurantId, int staffId);

        public Task<bool> HasAnyOtherActiveAssignmentAsync(int userId, int excludedUserRestaurantId);

        public Task<Domain.Entities.UserRestaurant?> GetActiveOwnerByRestaurantAsync(int restaurantId);

        public Task<List<Domain.Entities.UserRestaurant>> GetActiveByRestaurantAndRolesAsync(
            int restaurantId,
            IReadOnlyCollection<int> roleIds);
    }
}
