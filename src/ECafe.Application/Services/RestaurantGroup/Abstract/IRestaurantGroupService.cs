using ECafe.Application.DTOs.RestaurantGroup;

namespace ECafe.Application.Services.RestaurantGroup.Abstract
{
    public interface IRestaurantGroupService
    {
        Task<List<RestaurantGroupResponse>> GetAllAsync();

        Task<int> CreateAsync(CreateRestaurantGroupRequest request);
    }
}
