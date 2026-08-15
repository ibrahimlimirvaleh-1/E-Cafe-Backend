using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Services.Workflow.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Workflow;

public class GetWorkflowActionsQuery : IRequest<List<WorkflowActionResponse>>
{
    public string FlowCode { get; set; } = null!;

    public int StatusId { get; set; }

    public int? RestaurantId { get; set; }

    public int? EntityId { get; set; }

    public class Handler : IRequestHandler<GetWorkflowActionsQuery, List<WorkflowActionResponse>>
    {
        private readonly IWorkflowActionService _workflowActionService;

        public Handler(IWorkflowActionService workflowActionService)
        {
            _workflowActionService = workflowActionService;
        }

        public Task<List<WorkflowActionResponse>> Handle(GetWorkflowActionsQuery request, CancellationToken cancellationToken)
            => _workflowActionService.GetAvailableActionsAsync(
                request.FlowCode,
                request.StatusId,
                request.RestaurantId,
                request.EntityId);
    }
}
