using AutoMapper;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Restaurant.Concrete
{
    public class RestaurantManager : BaseManager, IRestaurantService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IEmailService _emailService;
        public RestaurantManager(IHttpContextAccessor httpContextAccessor,
                                 IMapper mapper, IConfiguration configuration,
                                 IRestaurantRepository restaurantRepository, IEmailService emailService)
                                 : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _emailService = emailService;
        }

        public async Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("request is not null!");

            var restaurant = new Domain.Entities.Restaurant
            {
                Name = request.Name,
                Location = request.Location,
                Phone = request.Phone,
                Email = request.Email,
                RatingAverage = request.RatingAverage,
                RatingCount = request.RatingCount,
                IsActive = true
            };

            await _restaurantRepository.Add(restaurant);
            await _restaurantRepository.SaveChangesAsync();

            await _emailService.SendMailAsync(restaurant.Email, restaurant.Name);

            return restaurant.Id;
        }
    }
}
