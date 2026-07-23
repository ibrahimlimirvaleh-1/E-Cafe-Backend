using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Restaurant.Abstract
{
    public interface IRestaurantService
    {
        public Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request);

        public Task UpdateRestaurantAsync(int restaurantId, UpdateRestaurantRequest request);

        public Task DeactivateRestaurantAsync(int restaurantId);

        public Task<PaginatedList<GetAllRestaurantsResponse>> GetAllRestaurantsAsync(
            PaginationFilter filter,
            string? search,
            string? location,
            string? cuisine);

        public Task<GetByIdRestaurantResponse> GetRestaurantAsync(int restaurantId);

        public Task<List<StaffDetailResponseDto>> GetRestaurantStaffAsync(int restaurantId);

        public Task<List<StaffPublicResponseDto>> GetRestaurantPublicStaffAsync(int restaurantId);

        Task<PaginatedList<PublicRestaurantListItemDto>> GetPublicRestaurantsAsync(
            PaginationFilter filter,
            string? search);

        Task<PublicRestaurantProfileDto> GetPublicRestaurantProfileAsync(int restaurantId);

        Task<List<PublicMenuCategoryDto>> GetPublicRestaurantMenuAsync(int restaurantId);

        Task<List<PublicStaffDto>> GetPublicRestaurantStaffAsync(int restaurantId);

        Task<List<PublicTableDto>> GetPublicRestaurantTablesAsync(int restaurantId);
    }
}
