using ECafe.Application.DTOs.Workflow;

namespace ECafe.Application.Services.Workflow.Abstract;

public interface IWorkflowActionService
{
    Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(
        string flowCode,
        int statusId,
        int? restaurantId,
        int? entityId);
}
