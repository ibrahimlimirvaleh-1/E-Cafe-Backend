using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Restaurant.GetById
{
    public class GetRestaurantQuery : IRequest<GetByIdRestaurantResponse>
    {
        public int RestaurantId { get; set; }

        public GetRestaurantQuery(int restaurantId)
        {
            RestaurantId = restaurantId;
        }

        public class GetRestaurantHandler : IRequestHandler<GetRestaurantQuery, GetByIdRestaurantResponse>
        {
            private readonly IRestaurantService _restaurantService;

            public GetRestaurantHandler(IRestaurantService restaurantService)
            {
                _restaurantService = restaurantService;
            }
            public async Task<GetByIdRestaurantResponse> Handle(GetRestaurantQuery request, CancellationToken cancellationToken)
            => await _restaurantService.GetRestaurantAsync(request.RestaurantId);

        }
    }
}
