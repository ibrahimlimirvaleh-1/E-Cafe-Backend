using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repository;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Workflow.Concrete;

public class WorkflowActionManager : BaseManager, IWorkflowActionService
{
    private readonly IBaseRepository<Domain.Entities.WorkflowActionRule> _workflowActionRuleRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly IReservationRepository _reservationRepository;

    public WorkflowActionManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        IBaseRepository<Domain.Entities.WorkflowActionRule> workflowActionRuleRepository,
        IUserRestaurantRepository userRestaurantRepository,
        IReservationRepository reservationRepository)
        : base(httpContextAccessor, mapper, configuration)
    {
        _workflowActionRuleRepository = workflowActionRuleRepository;
        _userRestaurantRepository = userRestaurantRepository;
        _reservationRepository = reservationRepository;
    }

    public async Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(
        string flowCode,
        int statusId,
        int? restaurantId,
        int? entityId)
    {
        if (string.IsNullOrWhiteSpace(flowCode) || statusId <= 0)
            return [];

        var normalizedFlowCode = NormalizeFlowCode(flowCode);
        var roleId = restaurantId.HasValue
            ? GetCurrentRoleId(restaurantId.Value)
            : GetCurrentRoleId();
        var isOwnedCustomerReservation = await IsOwnedCustomerReservationContextAsync(
            normalizedFlowCode,
            roleId,
            restaurantId,
            entityId);

        if (roleId == (int)RoleCode.Customer && !isOwnedCustomerReservation)
            return [];

        if (restaurantId.HasValue && !isOwnedCustomerReservation)
            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);

        if (!await IsCurrentUserAllowedForWorkflowContextAsync(restaurantId, roleId))
            return [];

        var rules = await _workflowActionRuleRepository.Query(rule =>
                rule.FlowCode == normalizedFlowCode &&
                rule.StatusId == statusId &&
                rule.RoleId == roleId &&
                rule.IsEnabled)
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync();

        if (restaurantId.HasValue &&
            entityId.HasValue &&
            normalizedFlowCode == WorkflowFlowCode.FromStatusType(StatusType.Reservation) &&
            await IsReservationTimeAlreadyPassedAsync(restaurantId.Value, entityId.Value))
        {
            rules = rules
                .Where(rule => !string.Equals(
                    rule.ActionCode,
                    "approvePaymentProof",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (restaurantId.HasValue &&
            entityId.HasValue &&
            normalizedFlowCode == WorkflowFlowCode.FromStatusType(StatusType.Reservation) &&
            !await IsReservationCheckInWindowOpenAsync(restaurantId.Value, entityId.Value))
        {
            rules = rules
                .Where(rule => !string.Equals(
                    rule.ActionCode,
                    "checkIn",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (roleId == (int)RoleCode.Customer &&
            restaurantId.HasValue &&
            entityId.HasValue &&
            normalizedFlowCode == WorkflowFlowCode.FromStatusType(StatusType.Reservation) &&
            await IsReservationCancellationDeadlinePassedAsync(restaurantId.Value, entityId.Value))
        {
            rules = rules
                .Where(rule => !string.Equals(
                    rule.ActionCode,
                    "cancel",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return rules
            .Select(rule => new WorkflowActionResponse
            {
                Code = rule.ActionCode,
                Label = rule.Label,
                HttpMethod = rule.HttpMethod,
                Endpoint = BuildActionEndpoint(rule.EndpointTemplate, restaurantId, entityId),
                RequiresConfirmation = rule.RequiresConfirmation,
                RequiresReason = ActionRequiresReason(rule.ActionCode),
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

        var normalizedFlowCode = NormalizeFlowCode(flowCode);
        var roleId = restaurantId.HasValue
            ? GetCurrentRoleId(restaurantId.Value)
            : GetCurrentRoleId();
        var isOwnedCustomerReservation = await IsOwnedCustomerReservationContextAsync(
            normalizedFlowCode,
            roleId,
            restaurantId,
            entityId);

        if (roleId == (int)RoleCode.Customer && !isOwnedCustomerReservation)
            throw new ForbiddenException("Workflow action is not allowed for this reservation.");

        if (restaurantId.HasValue && !isOwnedCustomerReservation)
            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);

        if (!await IsCurrentUserAllowedForWorkflowContextAsync(restaurantId, roleId))
            throw new ForbiddenException("Workflow action is not allowed for this user.");

        var normalizedActionCode = actionCode.Trim();

        if (restaurantId.HasValue &&
            entityId.HasValue &&
            string.Equals(normalizedFlowCode, WorkflowFlowCode.FromStatusType(StatusType.Reservation), StringComparison.Ordinal) &&
            string.Equals(normalizedActionCode, "checkIn", StringComparison.OrdinalIgnoreCase) &&
            !await IsReservationCheckInWindowOpenAsync(restaurantId.Value, entityId.Value))
        {
            throw new ForbiddenException("Rezervasiya hazırda check-in üçün uyğun vaxtda deyil.");
        }

        if (roleId == (int)RoleCode.Customer &&
            string.Equals(normalizedFlowCode, WorkflowFlowCode.FromStatusType(StatusType.Reservation), StringComparison.Ordinal) &&
            string.Equals(normalizedActionCode, "cancel", StringComparison.OrdinalIgnoreCase) &&
            restaurantId.HasValue &&
            entityId.HasValue &&
            await IsReservationCancellationDeadlinePassedAsync(restaurantId.Value, entityId.Value))
        {
            throw new ForbiddenException("Rezervasiyanı ləğv etmək üçün icazə verilən müddət bitib.");
        }

        var exists = await _workflowActionRuleRepository.CheckExistAsync(rule =>
            rule.FlowCode == normalizedFlowCode &&
            rule.StatusId == statusId &&
            rule.RoleId == roleId &&
            rule.ActionCode == normalizedActionCode &&
            rule.IsEnabled);

        if (!exists)
            throw new ForbiddenException("Workflow action is not allowed in the current state.");
    }

    private async Task<bool> IsCurrentUserAllowedForWorkflowContextAsync(int? restaurantId, int roleId)
    {
        if (!restaurantId.HasValue || roleId != (int)RoleCode.Owner)
            return true;

        var owner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(restaurantId.Value);
        return owner?.UserId == GetCurrentUserId();
    }

    private async Task<bool> IsOwnedCustomerReservationContextAsync(
        string normalizedFlowCode,
        int roleId,
        int? restaurantId,
        int? entityId)
    {
        if (roleId != (int)RoleCode.Customer ||
            normalizedFlowCode != WorkflowFlowCode.FromStatusType(StatusType.Reservation) ||
            !restaurantId.HasValue ||
            !entityId.HasValue)
        {
            return false;
        }

        var userId = GetCurrentUserId();
        return await _reservationRepository.Query(reservation =>
                reservation.Id == entityId.Value &&
                reservation.RestaurantId == restaurantId.Value &&
                reservation.CustomerUserId == userId)
            .AnyAsync();
    }

    private static string NormalizeFlowCode(string flowCode)
        => flowCode.Trim().ToLowerInvariant();

    private static bool ActionRequiresReason(string actionCode)
        => string.Equals(actionCode, "rejectPaymentProof", StringComparison.OrdinalIgnoreCase);

    private Task<bool> IsReservationTimeAlreadyPassedAsync(int restaurantId, int reservationId)
        => _reservationRepository.Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.ReservedAt <= DateTime.UtcNow)
            .AnyAsync();

    private Task<bool> IsReservationCancellationDeadlinePassedAsync(int restaurantId, int reservationId)
        => _reservationRepository.Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.CancellationDeadline.HasValue &&
                reservation.CancellationDeadline.Value <= DateTime.UtcNow)
            .AnyAsync();

    private Task<bool> IsReservationCheckInWindowOpenAsync(int restaurantId, int reservationId)
        => _reservationRepository.Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.ReservedAt <= DateTime.UtcNow &&
                reservation.NoShowDeadlineAt >= DateTime.UtcNow)
            .AnyAsync();

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
