using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class ActiveRestaurantContractAuthorizationHandler
        : AuthorizationHandler<ActiveRestaurantContractRequirement>
    {
        private readonly IRestaurantContractService _restaurantContractService;

        public ActiveRestaurantContractAuthorizationHandler(IRestaurantContractService restaurantContractService)
        {
            _restaurantContractService = restaurantContractService;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ActiveRestaurantContractRequirement requirement)
        {
            var roleId = GetIntClaim(context.User, ClaimTypes.Role);
            if (roleId is (int)RoleCode.SuperAdmin or (int)RoleCode.Customer)
            {
                context.Succeed(requirement);
                return;
            }

            var httpContext = context.Resource as HttpContext;
            var restaurantId = ResolveRestaurantId(httpContext, context.User, requirement.RestaurantIdKey);

            if (!restaurantId.HasValue)
                throw new ForbiddenException("Restaurant context is required.");

            try
            {
                await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId.Value);
                context.Succeed(requirement);
            }
            catch (BusinessRuleException ex)
            {
                throw new ForbiddenException(ex.Message);
            }
        }

        private static int? ResolveRestaurantId(HttpContext? httpContext, ClaimsPrincipal user, string restaurantIdKey)
        {
            if (httpContext is not null)
            {
                var routeValues = httpContext.GetRouteData()?.Values;

                if (routeValues is not null
                    && TryReadInt(routeValues[restaurantIdKey], out var routeRestaurantId))
                {
                    return routeRestaurantId;
                }

                if (httpContext.Request.Query.TryGetValue(restaurantIdKey, out var queryValue)
                    && TryReadInt(queryValue.FirstOrDefault(), out var queryRestaurantId))
                {
                    return queryRestaurantId;
                }
            }

            return GetIntClaim(user, "restaurantId");
        }

        private static int? GetIntClaim(ClaimsPrincipal user, string claimType)
            => TryReadInt(user.FindFirst(claimType)?.Value, out var value) ? value : null;

        private static bool TryReadInt(object? value, out int result)
            => int.TryParse(value?.ToString(), out result) && result > 0;
    }
}
