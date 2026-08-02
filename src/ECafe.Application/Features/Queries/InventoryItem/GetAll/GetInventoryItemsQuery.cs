using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Services.InventoryItem.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.InventoryItem.GetAll
{
    public class GetInventoryItemsQuery : IRequest<PaginatedList<InventoryItemDto>>
    {
        public int RestaurantId { get; set; }
        public string? Search { get; set; }
        public bool OnlyLowStock { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class GetInventoryItemsQueryHandler
        : IRequestHandler<GetInventoryItemsQuery, PaginatedList<InventoryItemDto>>
    {
        private readonly IInventoryItemService _inventoryItemService;

        public GetInventoryItemsQueryHandler(IInventoryItemService inventoryItemService)
        {
            _inventoryItemService = inventoryItemService;
        }

        public Task<PaginatedList<InventoryItemDto>> Handle(
            GetInventoryItemsQuery request,
            CancellationToken cancellationToken)
        {
            var filter = new PaginationFilter(request.PageNumber, request.PageSize);

            return _inventoryItemService.ListAsync(
                filter,
                request.Search,
                request.OnlyLowStock,
                request.RestaurantId);
        }
    }
}
