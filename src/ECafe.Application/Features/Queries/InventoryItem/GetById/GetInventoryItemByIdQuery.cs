using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.InventoryItem.GetById
{
    public class GetInventoryItemByIdQuery : IRequest<InventoryItemDto>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
    }

    public class GetInventoryItemByIdQueryHandler
        : IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDto>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public GetInventoryItemByIdQueryHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<InventoryItemDto> Handle(
            GetInventoryItemByIdQuery request,
            CancellationToken cancellationToken)
        {
            return _inventoryItemService.GetByIdAsync(
                request.RestaurantId,
                request.InventoryItemId);
        }
    }
}
