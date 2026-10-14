using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.TableSessions.Concrete;

public sealed class TableSessionManager : BaseManager, ITableSessionService
{
    private readonly ITableSessionRepository _sessions;
    private readonly IUserRestaurantRepository _assignments;

    public TableSessionManager(IHttpContextAccessor httpContextAccessor, IMapper mapper,
        IConfiguration configuration, ITableSessionRepository sessions, IUserRestaurantRepository assignments)
        : base(httpContextAccessor, mapper, configuration)
    {
        _sessions = sessions;
        _assignments = assignments;
    }

    public async Task<PaginatedList<WaiterTableSessionResponse>> GetMyOpenSessionsAsync(
        int restaurantId, PaginationFilter pagination, CancellationToken cancellationToken = default)
    {
        var waiterUserId = await RequireActiveWaiterAsync(restaurantId);
        return await _sessions.GetOpenForWaiterAsync(restaurantId, waiterUserId,
            PaginationFilterNormalizer.Normalize(pagination), cancellationToken);
    }

    public async Task<WaiterTableSessionResponse> GetMyOpenSessionAsync(
        int restaurantId, int tableSessionId, CancellationToken cancellationToken = default)
    {
        var waiterUserId = await RequireActiveWaiterAsync(restaurantId);
        if (tableSessionId <= 0)
            throw new BadRequestException(ErrorCode.InvalidTableSessionId);

        return await _sessions.GetOpenByIdForWaiterAsync(restaurantId, waiterUserId, tableSessionId, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>
    /// Restoran kontekstini və bazadakı aktiv ofisiant təyinatını yoxlayır; köhnə token roluna etibar etmir.
    /// Sessiya sahibinin ID-si sorğudan deyil, giriş etmiş istifadəçidən götürülür.
    /// </summary>
    private async Task<int> RequireActiveWaiterAsync(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BadRequestException(ErrorCode.InvalidRestaurantId);

        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        var userId = GetCurrentUserId();
        if (await _assignments.GetActiveRoleIdAsync(userId, restaurantId) != (int)RoleCode.Waiter)
            throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);

        return userId;
    }
}
