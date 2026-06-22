using ECafe.Application.DTOs.Item;

namespace ECafe.Application.Services.Item.Abstract
{
    public interface IItemService
    {
        public Task<int> CreateAsync(CreateItemRequest request);
    }
}
