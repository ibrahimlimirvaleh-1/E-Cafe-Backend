using ECafe.Application.DTOs.Restaurant;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class UpdateRestaurantCommand : UpdateRestaurantRequest, IRequest
    {
        public int RestaurantId { get; set; }
    }
}
