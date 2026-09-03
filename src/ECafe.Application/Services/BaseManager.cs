using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;


namespace ECafe.Application.Services;

public abstract class BaseManager
{
    private const string ActiveRestaurantIdHeader = "X-Active-Restaurant-Id";

    protected readonly IHttpContextAccessor HttpContextAccessor;
    protected readonly IMapper Mapper;
    protected readonly IConfiguration _configuration;

    protected BaseManager(IHttpContextAccessor httpContextAccessor, IMapper mapper, IConfiguration configuration)
    {
        HttpContextAccessor = httpContextAccessor;
        Mapper = mapper;
        _configuration = configuration;
    }

    protected bool IsCurrentUserSuperAdmin()
        => GetCurrentRoleId() == (int)RoleCode.SuperAdmin;

    protected int? GetCurrentRestaurantId()
    {
        var activeRestaurantId = GetActiveRestaurantIdFromHeader();
        if (activeRestaurantId.HasValue && GetCurrentRestaurantIds().Contains(activeRestaurantId.Value))
            return activeRestaurantId.Value;

        var legacyRestaurantIdClaim = CurrentUser.FindFirst("restaurantId")?.Value;
        if (int.TryParse(legacyRestaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0)
            return legacyRestaurantId;

        var restaurantIds = GetCurrentRestaurantIds();
        return restaurantIds.Count > 0 ? restaurantIds.First() : null;
    }

    protected int GetRequiredCurrentRestaurantId()
        => GetCurrentRestaurantId()
           ?? throw new ForbiddenException("Restaurant context is required.");

    protected int GetCurrentUserId()
    {
        var userIdClaim = CurrentUser.FindFirst("userId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            throw new ForbiddenException("User context is required.");

        return userId;
    }

    protected string? GetCurrentSessionId()
    {
        var sessionId = CurrentUser.FindFirst("sessionId")?.Value;
        return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
    }

    protected void EnsureCurrentUserCanAccessRestaurant(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        if (IsCurrentUserSuperAdmin())
            return;

        var currentRestaurantId = GetCurrentRestaurantId();
        if (!currentRestaurantId.HasValue || currentRestaurantId.Value != restaurantId)
            throw new ForbiddenException("You do not have access to this restaurant.");
    }

    protected IReadOnlyCollection<int> GetCurrentRestaurantIds()
    {
        var restaurantIdsClaim = CurrentUser.FindFirst("restaurantIds")?.Value;
        if (!string.IsNullOrWhiteSpace(restaurantIdsClaim))
        {
            return restaurantIdsClaim
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var restaurantId) ? restaurantId : 0)
                .Where(restaurantId => restaurantId > 0)
                .Distinct()
                .ToList();
        }

        var restaurantIdClaim = CurrentUser.FindFirst("restaurantId")?.Value;
        return int.TryParse(restaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0
            ? [legacyRestaurantId]
            : [];
    }

    private int? GetActiveRestaurantIdFromHeader()
    {
        var request = HttpContextAccessor.HttpContext?.Request;
        if (request is null ||
            !request.Headers.TryGetValue(ActiveRestaurantIdHeader, out var headerValue) ||
            !int.TryParse(headerValue.FirstOrDefault(), out var restaurantId) ||
            restaurantId <= 0)
        {
            return null;
        }

        return restaurantId;
    }

    private ClaimsPrincipal CurrentUser
        => HttpContextAccessor.HttpContext?.User
           ?? throw new ForbiddenException("Authenticated user context is required.");

    protected int GetCurrentRoleId()
    {
        var roleClaim = CurrentUser.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(roleClaim, out var roleId))
            throw new ForbiddenException("Role context is required.");

        return roleId;
    }

    protected int GetCurrentRoleId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        var restaurantRolesClaim = CurrentUser.FindFirst("restaurantRoles")?.Value;
        if (!string.IsNullOrWhiteSpace(restaurantRolesClaim))
        {
            foreach (var assignment in restaurantRolesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = assignment.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var assignedRestaurantId) &&
                    int.TryParse(parts[1], out var roleId) &&
                    assignedRestaurantId == restaurantId)
                {
                    return roleId;
                }
            }
        }

        return GetCurrentRoleId();
    }
}

