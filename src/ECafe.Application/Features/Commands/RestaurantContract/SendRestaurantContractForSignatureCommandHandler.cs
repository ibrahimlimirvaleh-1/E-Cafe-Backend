using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    public class SendRestaurantContractForSignatureCommandHandler
        : IRequestHandler<SendRestaurantContractForSignatureCommand>
    {
        private readonly IRestaurantContractService _contractService;

        public SendRestaurantContractForSignatureCommandHandler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public async Task Handle(
            SendRestaurantContractForSignatureCommand request,
            CancellationToken cancellationToken)
            => await _contractService.SendForSignatureAsync(request.RestaurantId, request.ContractId);
    }
}
