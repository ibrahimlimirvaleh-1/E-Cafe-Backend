using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionCacheService _cache;
        public PermissionAuthorizationHandler(IPermissionCacheService cache)
        {
            _cache = cache;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == null)
                return;

            var permissions = await _cache.GetPermissionsAsync(int.Parse(role));

            if (permissions.Contains(requirement.PermissionId))
            {
                context.Succeed(requirement);
            }
        }
    }
}