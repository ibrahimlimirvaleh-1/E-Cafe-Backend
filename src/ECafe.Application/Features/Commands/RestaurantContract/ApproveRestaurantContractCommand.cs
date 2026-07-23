using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class ApproveRestaurantContractCommand : IRequest
    {
        public int RestaurantId { get; set; }

        public int ContractId { get; set; }

        public bool HasAcceptedContractTerms { get; set; }

        public string? AcceptanceText { get; set; }
    }
}
