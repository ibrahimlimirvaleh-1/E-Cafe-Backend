using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class UpdateRestaurantContractCommandHandler : IRequestHandler<UpdateRestaurantContractCommand>
    {
        private readonly IRestaurantContractService _contractService;

        public UpdateRestaurantContractCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public Task Handle(UpdateRestaurantContractCommand request, CancellationToken cancellationToken)
            => _contractService.UpdateAsync(request.RestaurantId, request.ContractId, request);
    }
}
