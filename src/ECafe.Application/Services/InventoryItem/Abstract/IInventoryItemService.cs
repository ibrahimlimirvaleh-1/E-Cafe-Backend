using ECafe.Application.DTOs.InventoryItem;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.InventoryItem.Abstract
{
    public interface IInventoryItemService
    {
        Task<InventoryItemDto> CreateAsync(int restaurantId, CreateInventoryItemRequest request);
        Task<PaginatedList<InventoryItemDto>> ListAsync(PaginationFilter paginationFilter, string? search, bool onlyLowStock, int restaurantId);

        Task<InventoryItemDto> GetByIdAsync(int restaurantId, int inventoryItemId);

        Task<InventoryItemDto> UpdateAsync(UpdateInventoryItemRequest request, int restaurantId, int inventoryItemId);

        Task<DeleteOrDeactivateResponse> ActivateAsync(int restaurantId, int inventoryItemId);

        Task<DeleteOrDeactivateResponse> DeleteAsync(int restaurantId, int inventoryItemId);

        Task<DeleteOrDeactivateResponse> DeActivateAsync(int restaurantId, int inventoryItemId);
    }
}
