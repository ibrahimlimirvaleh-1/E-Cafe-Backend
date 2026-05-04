using System.Security.Claims;

namespace ECafe.Shared.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst("userId");

            if (userIdClaim == null)
                throw new UnauthorizedAccessException("UserId claim not found");

            return int.Parse(userIdClaim.Value);
        }

        public static int GetRoleId(this ClaimsPrincipal user)
        {
            var roleClaim = user.FindFirst(ClaimTypes.Role);

            if (roleClaim == null)
                throw new UnauthorizedAccessException("Role claim not found");

            return int.Parse(roleClaim.Value);
        }
    }
}
