using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.RestaurantContract
{
    public class GetPagedRestaurantContractsQuery : RestaurantContractFilterRequest, IRequest<PaginatedList<RestaurantContractResponse>>
    {
        public int RestaurantId { get; set; }

        public class Handler : IRequestHandler<GetPagedRestaurantContractsQuery, PaginatedList<RestaurantContractResponse>>
        {
            private readonly IRestaurantContractService _contractService;

            public Handler(IRestaurantContractService contractService)
            {
                _contractService = contractService;
            }

            public Task<PaginatedList<RestaurantContractResponse>> Handle(
                GetPagedRestaurantContractsQuery request,
                CancellationToken cancellationToken)
            {
                return _contractService.GetPagedByRestaurantAsync(request.RestaurantId, request);
            }
        }
    }
}
