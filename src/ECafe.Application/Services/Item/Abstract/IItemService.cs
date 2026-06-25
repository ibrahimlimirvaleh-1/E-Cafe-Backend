using ECafe.Application.DTOs.Item;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Item.Abstract
{
    public interface IItemService
    {
        public Task<int> CreateAsync(CreateItemRequest request);

        public Task<GetAllItemResponse> GetAllAsync(PaginationFilter filter,);
    }
}
