using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Update
{
    public class UpdateInventoryItemCommandHandler : IRequestHandler<UpdateInventoryItemCommand, InventoryItemDto>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public UpdateInventoryItemCommandHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<InventoryItemDto> Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
            => _inventoryItemService.UpdateAsync(request, request.RestaurantId, request.InventoryItemId);
    }
}
