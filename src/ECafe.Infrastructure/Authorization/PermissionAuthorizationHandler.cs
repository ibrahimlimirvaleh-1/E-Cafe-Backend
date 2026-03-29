using System.Security.Claims;
using ECafe.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ECafeDbContext _context;

        public PermissionAuthorizationHandler(ECafeDbContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var roleIds = context.User.FindAll(ClaimTypes.Role)
                .Select(x => x.Value)
                .Where(x => int.TryParse(x, out _))
                .Select(int.Parse)
                .Distinct()
                .ToList();

            if (roleIds.Count == 0)
                return;

            var hasPermission = await _context.RolePermissions
                .AsNoTracking()
                .AnyAsync(x =>
                    roleIds.Contains(x.RoleId) &&
                    x.PermissionId == requirement.PermissionId);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}