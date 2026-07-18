using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.RestaurantContract
{
    public class GetActiveRestaurantContractQuery : IRequest<RestaurantContractResponse>
    {
        public int RestaurantId { get; set; }

        public GetActiveRestaurantContractQuery(int restaurantId)
        {
            RestaurantId = restaurantId;
        }

        public class Handler : IRequestHandler<GetActiveRestaurantContractQuery, RestaurantContractResponse>
        {
            private readonly IRestaurantContractService _contractService;

            public Handler(IRestaurantContractService contractService)
            {
                _contractService = contractService;
            }

            public Task<RestaurantContractResponse> Handle(GetActiveRestaurantContractQuery request, CancellationToken cancellationToken)
                => _contractService.GetActiveAsync(request.RestaurantId);
        }
    }
}
