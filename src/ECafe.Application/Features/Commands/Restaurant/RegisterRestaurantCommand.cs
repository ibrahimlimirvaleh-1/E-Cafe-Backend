using ECafe.Application.DTOs.Restaurant;
using MediatR;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public class RegisterRestaurantCommand : RegisterRestaurantRequest, IRequest<int>
    {
    }
}
