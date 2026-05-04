using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace ECafe.Infrastructure.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(PermissionCode permission)
        {
            Policy = $"Permission:{(int)permission}";
        }
    }
}