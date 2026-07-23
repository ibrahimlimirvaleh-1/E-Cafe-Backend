using ECafe.Application.DTOs.Item;
using ECafe.Application.Services.Item.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Item.GetAll
{
    public class GetAllItemsQuery : IRequest<GetAllItemResponse>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int RestaurantId { get; set; }
        public int CategoryId { get; set; }
        public int StatusId { get; set; }

        public class GetAllItemsQueryHandler : IRequestHandler<GetAllItemsQuery, GetAllItemResponse>
        {
            private readonly IItemService _itemService;

            public GetAllItemsQueryHandler(IItemService itemService)
            {
                _itemService = itemService;
            }

            public async Task<GetAllItemResponse> Handle(GetAllItemsQuery request, CancellationToken cancellationToken)
            {
                var filter = new PaginationFilter(request.PageNumber, request.PageSize);
                return await _itemService.GetAllAsync(filter, request.RestaurantId, request.CategoryId, request.StatusId);
            }
        }
    }
}
