using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.InventoryMovement.History
{
    public class GetInventoryMovementHistoryQuery : IRequest<PaginatedList<InventoryMovementHistoryResponse>>
    {
        public int RestaurantId { get; set; }
        public int InventoryItemId { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class GetInventoryMovementHistoryQueryHandler
        : IRequestHandler<GetInventoryMovementHistoryQuery, PaginatedList<InventoryMovementHistoryResponse>>
    {
        private readonly IInventoryMovementService _inventoryMovementService;

        public GetInventoryMovementHistoryQueryHandler(IInventoryMovementService inventoryMovementService)
        {
            _inventoryMovementService = inventoryMovementService;
        }

        public Task<PaginatedList<InventoryMovementHistoryResponse>> Handle(
            GetInventoryMovementHistoryQuery request,
            CancellationToken cancellationToken)
        {
            var filter = new PaginationFilter(request.PageNumber, request.PageSize);

            return _inventoryMovementService.HistoryAsync(
                request.InventoryItemId,
                request.RestaurantId,
                filter);
        }
    }
}
