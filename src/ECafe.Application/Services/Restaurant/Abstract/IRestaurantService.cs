using ECafe.Application.DTOs.Restaurant;

namespace ECafe.Application.Services.Restaurant.Abstract
{
    public interface IRestaurantService
    {
        public Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request);

        public Task<List<GetAllRestaurantsResponse>> GetAllRestaurantsAsync();

        public Task<GetAllRestaurantsResponse> GetRestaurantAsync(int restaurantId);
    }
}
