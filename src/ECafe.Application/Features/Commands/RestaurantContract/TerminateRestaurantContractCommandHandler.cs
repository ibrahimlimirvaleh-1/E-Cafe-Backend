using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class TerminateRestaurantContractCommandHandler : IRequestHandler<TerminateRestaurantContractCommand>
    {
        private readonly IRestaurantContractService _contractService;

        public TerminateRestaurantContractCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public async Task Handle(TerminateRestaurantContractCommand request, CancellationToken cancellationToken)
            => await _contractService.TerminateAsync(request.RestaurantId, request.ContractId);
    }
}
