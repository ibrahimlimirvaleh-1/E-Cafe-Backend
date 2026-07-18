using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class TerminateRestaurantContractCommand : IRequest
    {
        public int RestaurantId { get; set; }

        public int ContractId { get; set; }
    }
}
