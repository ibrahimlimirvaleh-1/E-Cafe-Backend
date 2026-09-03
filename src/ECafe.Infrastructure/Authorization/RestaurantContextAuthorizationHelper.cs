using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECafe.Infrastructure.Authorization;

internal static class RestaurantContextAuthorizationHelper
{
    private const string ActiveRestaurantIdHeader = "X-Active-Restaurant-Id";
    private const string RestaurantIdsClaim = "restaurantIds";
    private const string RestaurantRolesClaim = "restaurantRoles";
    private const string LegacyRestaurantIdClaim = "restaurantId";

    private static readonly string[] RestaurantIdKeys =
    [
        "restaurantId",
        "RestaurantId"
    ];

    public static int? ResolveRestaurantId(HttpContext? httpContext, ClaimsPrincipal user, string? preferredKey = null)
    {
        if (httpContext is not null)
        {
            var keys = BuildRestaurantIdKeys(preferredKey);
            var routeValues = httpContext.GetRouteData()?.Values;

            if (routeValues is not null)
            {
                foreach (var key in keys)
                {
                    if (routeValues.TryGetValue(key, out var routeValue) &&
                        TryReadPositiveInt(routeValue, out var routeRestaurantId))
                    {
                        return routeRestaurantId;
                    }
                }
            }

            if (httpContext.Request.Headers.TryGetValue(ActiveRestaurantIdHeader, out var headerValue) &&
                TryReadPositiveInt(headerValue.FirstOrDefault(), out var activeRestaurantId))
            {
                return activeRestaurantId;
            }

            foreach (var key in keys)
            {
                if (httpContext.Request.Query.TryGetValue(key, out var queryValue) &&
                    TryReadPositiveInt(queryValue.FirstOrDefault(), out var queryRestaurantId))
                {
                    return queryRestaurantId;
                }
            }
        }

        return GetIntClaim(user, LegacyRestaurantIdClaim);
    }

    public static bool TryResolveRestaurantRoleId(ClaimsPrincipal user, int restaurantId, out int roleId)
    {
        roleId = 0;

        var restaurantRolesClaim = user.FindFirst(RestaurantRolesClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(restaurantRolesClaim))
        {
            foreach (var assignment in restaurantRolesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = assignment.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var assignedRestaurantId) &&
                    int.TryParse(parts[1], out var assignedRoleId) &&
                    assignedRestaurantId == restaurantId &&
                    assignedRoleId > 0)
                {
                    roleId = assignedRoleId;
                    return true;
                }
            }
        }

        var legacyRestaurantId = GetIntClaim(user, LegacyRestaurantIdClaim);
        if (legacyRestaurantId == restaurantId &&
            TryReadPositiveInt(user.FindFirst(ClaimTypes.Role)?.Value, out var legacyRoleId))
        {
            roleId = legacyRoleId;
            return true;
        }

        return false;
    }

    public static int? GetIntClaim(ClaimsPrincipal user, string claimType)
        => TryReadPositiveInt(user.FindFirst(claimType)?.Value, out var value) ? value : null;

    private static IReadOnlyCollection<string> BuildRestaurantIdKeys(string? preferredKey)
    {
        if (string.IsNullOrWhiteSpace(preferredKey))
            return RestaurantIdKeys;

        return RestaurantIdKeys
            .Prepend(preferredKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryReadPositiveInt(object? value, out int result)
        => int.TryParse(value?.ToString(), out result) && result > 0;
}
