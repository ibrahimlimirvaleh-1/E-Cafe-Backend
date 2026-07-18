using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class CreateRestaurantContractCommandHandler : IRequestHandler<CreateRestaurantContractCommand, int>
    {
        private readonly IRestaurantContractService _contractService;

        public CreateRestaurantContractCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public Task<int> Handle(CreateRestaurantContractCommand request, CancellationToken cancellationToken)
            => _contractService.CreateAsync(request.RestaurantId, request);
    }
}
