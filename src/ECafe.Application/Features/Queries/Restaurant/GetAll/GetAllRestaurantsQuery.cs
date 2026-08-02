using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Restaurant.GetAll
{
    public class GetAllRestaurantsQuery : IRequest<PaginatedList<GetAllRestaurantsResponse>>
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? Search { get; set; }
        public string? Location { get; set; }
        public string? Cuisine { get; set; }

        public class GetAllRestaurantsQueryHandler : IRequestHandler<GetAllRestaurantsQuery, PaginatedList<GetAllRestaurantsResponse>>
        {
            private readonly IRestaurantService _restaurantService;

            public GetAllRestaurantsQueryHandler(IRestaurantService restaurantService)
            {
                _restaurantService = restaurantService;
            }

            public Task<PaginatedList<GetAllRestaurantsResponse>> Handle(GetAllRestaurantsQuery request, CancellationToken cancellationToken)
            {
                var filter = new PaginationFilter(request.PageNumber, request.PageSize);

                return _restaurantService.GetAllRestaurantsAsync(
                    filter,
                    request.Search,
                    request.Location,
                    request.Cuisine);
            }
        }
    }
}
