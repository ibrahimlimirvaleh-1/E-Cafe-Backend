using ECafe.Application.DTOs.InventoryItem;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Update
{
    public class UpdateInventoryItemCommand : UpdateInventoryItemRequest, IRequest<InventoryItemDto>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
    }
}
