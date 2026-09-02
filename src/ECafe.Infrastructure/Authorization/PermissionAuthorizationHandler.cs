using System.Security.Claims;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace ECafe.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionCacheService _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRestaurantAccessCache _userRestaurantAccessCache;

    public PermissionAuthorizationHandler(
        IPermissionCacheService cache,
        IHttpContextAccessor httpContextAccessor,
        IUserRestaurantAccessCache userRestaurantAccessCache)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _userRestaurantAccessCache = userRestaurantAccessCache;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roleId = await ResolveRoleIdForPermissionCheckAsync(context);
        if (!roleId.HasValue)
            return;

        var permissions = await _cache.GetPermissionsAsync(roleId.Value);
        if (permissions.Contains(requirement.PermissionId))
            context.Succeed(requirement);
    }

    private async Task<int?> ResolveRoleIdForPermissionCheckAsync(AuthorizationHandlerContext context)
    {
        var globalRoleId = RestaurantContextAuthorizationHelper.GetIntClaim(context.User, ClaimTypes.Role);
        if (!globalRoleId.HasValue)
            return null;

        var httpContext = context.Resource as HttpContext ?? _httpContextAccessor.HttpContext;
        var restaurantId = RestaurantContextAuthorizationHelper.ResolveRestaurantId(httpContext, context.User);

        if (!restaurantId.HasValue)
            return globalRoleId;

        if (globalRoleId.Value == (int)RoleCode.SuperAdmin)
            return globalRoleId;

        var userId = RestaurantContextAuthorizationHelper.GetIntClaim(context.User, "userId");
        if (!userId.HasValue)
            return null;

        return await _userRestaurantAccessCache.GetActiveRoleIdAsync(userId.Value, restaurantId.Value);
    }
}
