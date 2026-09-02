using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
            var restaurantId = RestaurantContextAuthorizationHelper.ResolveRestaurantId(
                httpContext,
                context.User,
                requirement.RestaurantIdKey);

            if (!restaurantId.HasValue)
                throw new ForbiddenException("Restaurant context is required.");

            try
            {
                await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId.Value);
                context.Succeed(requirement);
            }
            catch (BusinessRuleException ex)
            {
                throw new ForbiddenException(ex.Code, ex.Parameters);
            }
        }

        private static int? GetIntClaim(ClaimsPrincipal user, string claimType)
            => TryReadInt(user.FindFirst(claimType)?.Value, out var value) ? value : null;

        private static bool TryReadInt(object? value, out int result)
            => int.TryParse(value?.ToString(), out result) && result > 0;
    }
}
