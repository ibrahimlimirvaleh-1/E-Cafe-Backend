using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.User.Staff;

namespace ECafe.Application.Services.Restaurant.Abstract
{
    public interface IRestaurantService
    {
        public Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request);

        public Task UpdateRestaurantAsync(int restaurantId, UpdateRestaurantRequest request);

        public Task DeactivateRestaurantAsync(int restaurantId);

        public Task<List<GetAllRestaurantsResponse>> GetAllRestaurantsAsync();

        public Task<GetByIdRestaurantResponse> GetRestaurantAsync(int restaurantId);

        public Task<List<StaffDetailResponseDto>> GetRestaurantStaffAsync(int restaurantId);

        public Task<List<StaffPublicResponseDto>> GetRestaurantPublicStaffAsync(int restaurantId);

    }
}
