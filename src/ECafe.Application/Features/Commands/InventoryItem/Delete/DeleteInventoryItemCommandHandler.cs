using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Delete
{
    public class DeleteInventoryItemCommandHandler
        : IRequestHandler<DeleteInventoryItemCommand, DeleteOrDeactivateResponse>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public DeleteInventoryItemCommandHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(
            DeleteInventoryItemCommand request,
            CancellationToken cancellationToken)
        {
            return _inventoryItemService.DeleteAsync(request.RestaurantId, request.InventoryItemId);
        }
    }
}
