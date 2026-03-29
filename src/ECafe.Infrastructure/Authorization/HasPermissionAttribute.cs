using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        private const string PolicyPrefix = "Permission";

        public HasPermissionAttribute(PermissionCode permission)
        {
            Policy = $"{PolicyPrefix}:{(int)permission}";
        }
    }
}