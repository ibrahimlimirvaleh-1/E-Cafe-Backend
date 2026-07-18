using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.RestaurantContract
{
    public class GetRestaurantContractsQuery : IRequest<List<RestaurantContractResponse>>
    {
        public int RestaurantId { get; set; }

        public GetRestaurantContractsQuery(int restaurantId)
        {
            RestaurantId = restaurantId;
        }

        public class Handler : IRequestHandler<GetRestaurantContractsQuery, List<RestaurantContractResponse>>
        {
            private readonly IRestaurantContractService _contractService;

            public Handler(IRestaurantContractService contractService)
            {
                _contractService = contractService;
            }

            public Task<List<RestaurantContractResponse>> Handle(GetRestaurantContractsQuery request, CancellationToken cancellationToken)
                => _contractService.GetByRestaurantAsync(request.RestaurantId);
        }
    }
}
