using ECafe.Application.DTOs.Item;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Item.Abstract
{
    public interface IItemService
    {
        public Task<int> CreateAsync(CreateItemRequest request);

        public Task<int> UpdateAsync(int restaurantId, int itemId, UpdateItemRequest request);

        public Task<int> DeactivateAsync(int restaurantId, int itemId);

        public Task<int> DeleteAsync(int restaurantId, int itemId);

        public Task<GetAllItemResponse> GetAllAsync(PaginationFilter filter, int restaurantId, int categoryId, int statusId);
    }
}
