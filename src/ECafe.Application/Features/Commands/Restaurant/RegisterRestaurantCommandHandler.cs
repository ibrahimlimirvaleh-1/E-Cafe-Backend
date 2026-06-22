using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class RegisterRestaurantCommandHandler : IRequestHandler<RegisterRestaurantCommand, int>
    {
        private readonly IRestaurantService _restaurantService;

        public RegisterRestaurantCommandHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public async Task<int> Handle(RegisterRestaurantCommand request, CancellationToken cancellationToken)
        {
            return await _restaurantService.RegisterRestaurantAsync(request);
        }
    }
}
