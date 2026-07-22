using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class ActivateRestaurantContractCommandHandler : IRequestHandler<ActivateRestaurantContractCommand>
    {
        private readonly IRestaurantContractService _contractService;

        public ActivateRestaurantContractCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public async Task Handle(ActivateRestaurantContractCommand request, CancellationToken cancellationToken)
            => await _contractService.ActivateAsync(request.RestaurantId, request.ContractId);
    }
}
