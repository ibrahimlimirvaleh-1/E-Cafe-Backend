using ECafe.Application.DTOs.InventoryItem;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryItem.Activate
{
    public class ActivateInventoryItemCommand : IRequest<DeleteOrDeactivateResponse>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
    }
}
