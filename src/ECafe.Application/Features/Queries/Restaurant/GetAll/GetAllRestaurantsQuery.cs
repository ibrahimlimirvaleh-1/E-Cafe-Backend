using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Restaurant.GetAll
{
    public class GetAllRestaurantsQuery : IRequest<List<GetAllRestaurantsResponse>>
    {
        public class GetAllRestaurantsQueryHandler : IRequestHandler<GetAllRestaurantsQuery, List<GetAllRestaurantsResponse>>
        {
            private readonly IRestaurantService _restaurantService;
            public GetAllRestaurantsQueryHandler(IRestaurantService restaurantService)
            {
                _restaurantService = restaurantService;
            }
            public async Task<List<GetAllRestaurantsResponse>> Handle(GetAllRestaurantsQuery request, CancellationToken cancellationToken)
            {
                return await _restaurantService.GetAllRestaurantsAsync();
            }
        }
    }
}
