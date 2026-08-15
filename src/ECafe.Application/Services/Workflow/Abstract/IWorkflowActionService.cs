using ECafe.Application.DTOs.Workflow;

namespace ECafe.Application.Services.Workflow.Abstract;

public interface IWorkflowActionService
{
    Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(
        string flowCode,
        int statusId,
        int? restaurantId,
        int? entityId);

    Task EnsureCanExecuteAsync(
        string flowCode,
        int statusId,
        string actionCode,
        int? restaurantId,
        int? entityId);
}
