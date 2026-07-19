using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class UpdateRestaurantCommandHandler : IRequestHandler<UpdateRestaurantCommand>
    {
        private readonly IRestaurantService _restaurantService;

        public UpdateRestaurantCommandHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public async Task Handle(UpdateRestaurantCommand request, CancellationToken cancellationToken)
        {
            await _restaurantService.UpdateRestaurantAsync(request.RestaurantId, request);
        }
    }
}
