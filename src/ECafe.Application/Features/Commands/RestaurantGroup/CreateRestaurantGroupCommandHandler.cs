using ECafe.Application.Services.RestaurantGroup.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantGroup
{
    public class CreateRestaurantGroupCommandHandler : IRequestHandler<CreateRestaurantGroupCommand, int>
    {
        private readonly IRestaurantGroupService _restaurantGroupService;

        public CreateRestaurantGroupCommandHandler(IRestaurantGroupService restaurantGroupService)
        {
            _restaurantGroupService = restaurantGroupService;
        }

        public Task<int> Handle(CreateRestaurantGroupCommand request, CancellationToken cancellationToken)
            => _restaurantGroupService.CreateAsync(request);
    }
}
