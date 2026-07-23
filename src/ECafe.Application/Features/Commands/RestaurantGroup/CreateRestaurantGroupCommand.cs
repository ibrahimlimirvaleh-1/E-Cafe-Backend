using ECafe.Application.DTOs.RestaurantGroup;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantGroup
{
    public class CreateRestaurantGroupCommand : CreateRestaurantGroupRequest, IRequest<int>
    {
    }
}
