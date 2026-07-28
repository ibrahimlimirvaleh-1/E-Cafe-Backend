using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Create
{
    public class CreateInventoryItemCommandHandler : IRequestHandler<CreateInventoryItemCommand, InventoryItemDto>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public CreateInventoryItemCommandHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<InventoryItemDto> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
            => _inventoryItemService.CreateAsync(request.RestaurantId, request);
    }
}
