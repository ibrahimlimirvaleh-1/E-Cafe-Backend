using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Workflow.Concrete;

public class WorkflowActionManager : BaseManager, IWorkflowActionService
{
    private readonly IBaseRepository<Domain.Entities.WorkflowActionRule> _workflowActionRuleRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;

    public WorkflowActionManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        IBaseRepository<Domain.Entities.WorkflowActionRule> workflowActionRuleRepository,
        IUserRestaurantRepository userRestaurantRepository)
        : base(httpContextAccessor, mapper, configuration)
    {
        _workflowActionRuleRepository = workflowActionRuleRepository;
        _userRestaurantRepository = userRestaurantRepository;
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

        if (!await IsCurrentUserAllowedForWorkflowContextAsync(restaurantId))
            return [];

        var normalizedFlowCode = NormalizeFlowCode(flowCode);
        var roleId = restaurantId.HasValue
            ? GetCurrentRoleId(restaurantId.Value)
            : GetCurrentRoleId();
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

    public async Task EnsureCanExecuteAsync(
        string flowCode,
        int statusId,
        string actionCode,
        int? restaurantId,
        int? entityId)
    {
        if (string.IsNullOrWhiteSpace(flowCode) || statusId <= 0 || string.IsNullOrWhiteSpace(actionCode))
            throw new ForbiddenException("Workflow action is not allowed.");

        if (restaurantId.HasValue)
            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);

        if (!await IsCurrentUserAllowedForWorkflowContextAsync(restaurantId))
            throw new ForbiddenException("Workflow action is not allowed for this user.");

        var normalizedFlowCode = NormalizeFlowCode(flowCode);
        var normalizedActionCode = actionCode.Trim();
        var roleId = restaurantId.HasValue
            ? GetCurrentRoleId(restaurantId.Value)
            : GetCurrentRoleId();

        var exists = await _workflowActionRuleRepository.CheckExistAsync(rule =>
            rule.FlowCode == normalizedFlowCode &&
            rule.StatusId == statusId &&
            rule.RoleId == roleId &&
            rule.ActionCode == normalizedActionCode &&
            rule.IsEnabled);

        if (!exists)
            throw new ForbiddenException("Workflow action is not allowed in the current state.");
    }

    private async Task<bool> IsCurrentUserAllowedForWorkflowContextAsync(int? restaurantId)
    {
        if (!restaurantId.HasValue || GetCurrentRoleId(restaurantId.Value) != (int)RoleCode.Owner)
            return true;

        var owner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(restaurantId.Value);
        return owner?.UserId == GetCurrentUserId();
    }

    private static string NormalizeFlowCode(string flowCode)
        => flowCode.Trim().ToLowerInvariant();

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
