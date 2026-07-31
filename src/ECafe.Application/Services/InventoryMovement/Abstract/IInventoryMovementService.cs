using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.InventoryMovement.Abstract
{
    public interface IInventoryMovementService
    {
        Task<InventoryMovementResponse> CreateAsync(int inventoryItemId, int restaurantId, CreateInventoryMovementRequest request);

        Task<PaginatedList<InventoryMovementHistoryResponse>> HistoryAsync(int inventoryItemId,int restaurantId,PaginationFilter paginationFilter);

    }
}
