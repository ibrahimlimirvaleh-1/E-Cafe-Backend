using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Activate
{
    public class ActivateInventoryItemCommandHandler
        : IRequestHandler<ActivateInventoryItemCommand, DeleteOrDeactivateResponse>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public ActivateInventoryItemCommandHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(
            ActivateInventoryItemCommand request,
            CancellationToken cancellationToken)
        {
            return _inventoryItemService.ActivateAsync(
                request.RestaurantId,
                request.InventoryItemId);
        }
    }
}
