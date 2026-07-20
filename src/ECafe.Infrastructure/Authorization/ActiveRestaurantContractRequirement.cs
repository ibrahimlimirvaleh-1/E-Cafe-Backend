using Microsoft.AspNetCore.Authorization;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class ActiveRestaurantContractRequirement : IAuthorizationRequirement
    {
        public ActiveRestaurantContractRequirement(string restaurantIdKey)
        {
            RestaurantIdKey = restaurantIdKey;
        }

        public string RestaurantIdKey { get; }
    }
}
