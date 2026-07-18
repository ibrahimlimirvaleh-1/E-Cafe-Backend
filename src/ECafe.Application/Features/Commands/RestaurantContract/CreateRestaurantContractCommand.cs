using ECafe.Application.DTOs.RestaurantContract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class CreateRestaurantContractCommand : CreateRestaurantContractRequest, IRequest<int>
    {
        public int RestaurantId { get; set; }
    }
}
