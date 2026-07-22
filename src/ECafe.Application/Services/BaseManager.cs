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
        var restaurantIdClaim = CurrentUser.FindFirst("restaurantId")?.Value;
        return int.TryParse(restaurantIdClaim, out var restaurantId) && restaurantId > 0
            ? restaurantId
            : null;
    }

    protected int GetCurrentUserId()
    {
        var userIdClaim = CurrentUser.FindFirst("userId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            throw new ForbiddenException("User context is required.");

        return userId;
    }

    protected void EnsureCurrentUserCanAccessRestaurant(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        if (IsCurrentUserSuperAdmin())
            return;

        var currentRestaurantId = GetCurrentRestaurantId();
        if (currentRestaurantId != restaurantId)
            throw new ForbiddenException("You do not have access to this restaurant.");
    }

    private ClaimsPrincipal CurrentUser
        => HttpContextAccessor.HttpContext?.User
           ?? throw new ForbiddenException("Authenticated user context is required.");

    private int GetCurrentRoleId()
    {
        var roleClaim = CurrentUser.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(roleClaim, out var roleId))
            throw new ForbiddenException("Role context is required.");

        return roleId;
    }
}

