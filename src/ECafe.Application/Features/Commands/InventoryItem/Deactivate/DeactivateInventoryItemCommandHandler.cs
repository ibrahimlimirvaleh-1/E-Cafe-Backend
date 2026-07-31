using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Deactivate
{
    public class DeactivateInventoryItemCommandHandler
        : IRequestHandler<DeactivateInventoryItemCommand, DeleteOrDeactivateResponse>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public DeactivateInventoryItemCommandHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<DeleteOrDeactivateResponse> Handle(
            DeactivateInventoryItemCommand request,
            CancellationToken cancellationToken)
        {
            return _inventoryItemService.DeActivateAsync(request.RestaurantId, request.InventoryItemId);
        }
    }
}
