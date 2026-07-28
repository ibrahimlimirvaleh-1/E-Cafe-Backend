using ECafe.Application.DTOs.InventoryItem;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Create
{
    public class CreateInventoryItemCommand : CreateInventoryItemRequest, IRequest<InventoryItemDto>
    {
        public int RestaurantId { get; set; }
    }
}
