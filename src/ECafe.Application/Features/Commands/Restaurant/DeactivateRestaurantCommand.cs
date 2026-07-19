using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class DeactivateRestaurantCommand : IRequest
    {
        public int RestaurantId { get; set; }
    }
}
