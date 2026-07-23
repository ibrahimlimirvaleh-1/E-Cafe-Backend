using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.Services.RestaurantGroup.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.RestaurantGroup
{
    public class GetRestaurantGroupsQuery : IRequest<List<RestaurantGroupResponse>>
    {
        public class Handler : IRequestHandler<GetRestaurantGroupsQuery, List<RestaurantGroupResponse>>
        {
            private readonly IRestaurantGroupService _restaurantGroupService;

            public Handler(IRestaurantGroupService restaurantGroupService)
            {
                _restaurantGroupService = restaurantGroupService;
            }

            public Task<List<RestaurantGroupResponse>> Handle(GetRestaurantGroupsQuery request, CancellationToken cancellationToken)
                => _restaurantGroupService.GetAllAsync();
        }
    }
}
