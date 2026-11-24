using System.Security.Claims;
using ECafe.Shared.Http;
using ECafe.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECafe.Infrastructure.Authorization;

internal static class RestaurantContextAuthorizationHelper
{

    private static readonly string[] RestaurantIdKeys =
    [
        "restaurantId",
        "RestaurantId"
    ];

    /// <summary>Route, seçilmiş header, query və legacy claim-in əvvəlki prioriteti ilə restoran kontekstini oxuyur.</summary>
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

            if (httpContext.Request.Headers.TryGetValue(RequestHeaderNames.ActiveRestaurantId, out var headerValue) &&
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

        return GetIntClaim(user, AuthenticationClaimTypes.RestaurantId);
    }

    /// <summary>Verilmiş restoranın token rolunu və əvvəlki legacy rol fallback-ini eyni qaydada oxuyur.</summary>
    public static bool TryResolveRestaurantRoleId(ClaimsPrincipal user, int restaurantId, out int roleId)
    {
        roleId = 0;

        var restaurantRolesClaim = user.FindFirst(AuthenticationClaimTypes.RestaurantRoles)?.Value;
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

        var legacyRestaurantId = GetIntClaim(user, AuthenticationClaimTypes.RestaurantId);
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
