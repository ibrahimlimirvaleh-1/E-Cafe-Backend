using ECafe.Application.DTOs.InventoryMovement;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryMovement.Create
{
    public class CreateInventoryMovementCommand : CreateInventoryMovementRequest, IRequest<InventoryMovementResponse>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
    }
}
