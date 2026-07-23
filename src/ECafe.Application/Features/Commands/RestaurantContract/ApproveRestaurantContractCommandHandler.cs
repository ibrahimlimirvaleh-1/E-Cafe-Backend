using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class ApproveRestaurantContractCommandHandler
        : IRequestHandler<ApproveRestaurantContractCommand>
    {
        private readonly IRestaurantContractService _contractService;

        public ApproveRestaurantContractCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public async Task Handle(
            ApproveRestaurantContractCommand request,
            CancellationToken cancellationToken)
            => await _contractService.ApproveAsync(
                request.RestaurantId,
                request.ContractId,
                request.HasAcceptedContractTerms,
                request.AcceptanceText);
    }
}
