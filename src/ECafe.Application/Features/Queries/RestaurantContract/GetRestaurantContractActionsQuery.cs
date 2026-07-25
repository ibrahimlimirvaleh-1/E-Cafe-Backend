using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Services.RestaurantContract.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.RestaurantContract;

public class GetRestaurantContractActionsQuery : IRequest<List<WorkflowActionResponse>>
{
    public int RestaurantId { get; set; }

    public int ContractId { get; set; }

    public GetRestaurantContractActionsQuery(int restaurantId, int contractId)
    {
        RestaurantId = restaurantId;
        ContractId = contractId;
    }

    public class Handler : IRequestHandler<GetRestaurantContractActionsQuery, List<WorkflowActionResponse>>
    {
        private readonly IRestaurantContractService _contractService;

        public Handler(IRestaurantContractService contractService)
        {
            _contractService = contractService;
        }

        public Task<List<WorkflowActionResponse>> Handle(GetRestaurantContractActionsQuery request, CancellationToken cancellationToken)
            => _contractService.GetAvailableActionsAsync(request.RestaurantId, request.ContractId);
    }
}
