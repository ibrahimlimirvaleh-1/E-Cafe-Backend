using Microsoft.AspNetCore.Authorization;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public int PermissionId { get; }

        public PermissionRequirement(int permissionId)
        {
            PermissionId = permissionId;
        }
    }
}