using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class ActivateRestaurantContractCommand : IRequest
    {
        public int RestaurantId { get; set; }

        public int ContractId { get; set; }

        public int? SignedByUserId { get; set; }
    }
}
