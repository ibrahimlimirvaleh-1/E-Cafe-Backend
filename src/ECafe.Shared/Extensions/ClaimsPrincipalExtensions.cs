using System.Security.Claims;

using ECafe.Shared.Security;

namespace ECafe.Shared.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>Mövcud userId claim-ini əvvəlki parse və çatışmayan claim xətası davranışı ilə oxuyur.</summary>
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(AuthenticationClaimTypes.UserId);

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

        /// <summary>Legacy restaurantId claim-ini əvvəlki parse və çatışmayan claim xətası davranışı ilə oxuyur.</summary>
        public static int GetRestaurantId(this ClaimsPrincipal user)
        {
            var restaurantClaim = user.FindFirst(AuthenticationClaimTypes.RestaurantId);

            if (restaurantClaim == null)
                throw new UnauthorizedAccessException("RestaurantId claim not found");

            return int.Parse(restaurantClaim.Value);
        }
    }
}
