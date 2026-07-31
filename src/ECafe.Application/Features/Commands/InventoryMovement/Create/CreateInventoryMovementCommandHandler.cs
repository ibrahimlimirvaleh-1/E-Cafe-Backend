using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Services.InventoryMovement.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.InventoryMovement.Create
{
    public class CreateInventoryMovementCommandHandler : IRequestHandler<CreateInventoryMovementCommand, InventoryMovementResponse>
    {
        private readonly IInventoryMovementService _inventoryMovementService;

        public CreateInventoryMovementCommandHandler(IInventoryMovementService inventoryMovementService)
        {
            _inventoryMovementService = inventoryMovementService;
        }

        public Task<InventoryMovementResponse> Handle(
            CreateInventoryMovementCommand request,
            CancellationToken cancellationToken)
        {
            return _inventoryMovementService.CreateAsync(
                request.InventoryItemId,
                request.RestaurantId,
                request);
        }
    }
}
