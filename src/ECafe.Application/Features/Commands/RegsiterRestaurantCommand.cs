using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands
{
    public class RegisterRestaurantCommand : IRequest<int>
    {
        public string Name { get; set; } = null!;
        public string Location { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Email { get; set; } = null!;
        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }
        public List<IFormFile>? Files { get; set; }


    }

    public class RegisterRestaurantCommandHandler : IRequestHandler<RegisterRestaurantCommand, int>
    {
        private readonly IRestaurantService _restaurantService;

        public RegisterRestaurantCommandHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public async Task<int> Handle(RegisterRestaurantCommand request, CancellationToken cancellationToken)
        {
            var dto = new RegisterRestaurantRequest
            {
                Name = request.Name,
                Location = request.Location,
                Phone = request.Phone,
                Email = request.Email,
                RatingAverage = request.RatingAverage,
                RatingCount = request.RatingCount,
                Files = request.Files
            };

            return await _restaurantService.RegisterRestaurantAsync(dto);
        }
    }
}