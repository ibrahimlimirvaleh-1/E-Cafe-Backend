using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class DeactivateRestaurantCommandHandler : IRequestHandler<DeactivateRestaurantCommand>
    {
        private readonly IRestaurantService _restaurantService;

        public DeactivateRestaurantCommandHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public async Task Handle(DeactivateRestaurantCommand request, CancellationToken cancellationToken)
        {
            await _restaurantService.DeactivateRestaurantAsync(request.RestaurantId);
        }
    }
}
