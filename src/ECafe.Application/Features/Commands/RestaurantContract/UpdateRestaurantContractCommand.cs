using ECafe.Application.DTOs.RestaurantContract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class UpdateRestaurantContractCommand : UpdateRestaurantContractRequest, IRequest
    {
        public int RestaurantId { get; set; }

        public int ContractId { get; set; }
    }
}
