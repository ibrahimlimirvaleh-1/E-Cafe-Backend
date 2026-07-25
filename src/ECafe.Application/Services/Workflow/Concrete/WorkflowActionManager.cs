using AutoMapper;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Repository;
using ECafe.Application.Services.Workflow.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Workflow.Concrete;

public class WorkflowActionManager : BaseManager, IWorkflowActionService
{
    private readonly IBaseRepository<Domain.Entities.WorkflowActionRule> _workflowActionRuleRepository;

    public WorkflowActionManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        IBaseRepository<Domain.Entities.WorkflowActionRule> workflowActionRuleRepository)
        : base(httpContextAccessor, mapper, configuration)
    {
        _workflowActionRuleRepository = workflowActionRuleRepository;
    }

    public async Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(
        string flowCode,
        int statusId,
        int? restaurantId,
        int? entityId)
    {
        if (string.IsNullOrWhiteSpace(flowCode) || statusId <= 0)
            return [];

        if (restaurantId.HasValue)
            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);

        var normalizedFlowCode = flowCode.Trim().ToLowerInvariant();
        var roleId = GetCurrentRoleId();
        var rules = await _workflowActionRuleRepository.Query(rule =>
                rule.FlowCode == normalizedFlowCode &&
                rule.StatusId == statusId &&
                rule.RoleId == roleId &&
                rule.IsEnabled)
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync();

        return rules
            .Select(rule => new WorkflowActionResponse
            {
                Code = rule.ActionCode,
                Label = rule.Label,
                HttpMethod = rule.HttpMethod,
                Endpoint = BuildActionEndpoint(rule.EndpointTemplate, restaurantId, entityId),
                RequiresConfirmation = rule.RequiresConfirmation,
                SortOrder = rule.SortOrder
            })
            .ToList();
    }

    private static string BuildActionEndpoint(string template, int? restaurantId, int? entityId)
    {
        var entityIdText = entityId?.ToString() ?? string.Empty;

        return template
            .Replace("{restaurantId}", restaurantId?.ToString() ?? string.Empty)
            .Replace("{contractId}", entityIdText)
            .Replace("{reservationId}", entityIdText)
            .Replace("{orderId}", entityIdText)
            .Replace("{paymentId}", entityIdText);
    }
}
