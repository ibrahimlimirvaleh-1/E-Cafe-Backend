using Microsoft.AspNetCore.Authorization;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class RequireActiveRestaurantContractAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "ActiveRestaurantContract";

        public RequireActiveRestaurantContractAttribute(string restaurantIdKey = "restaurantId")
        {
            Policy = $"{PolicyPrefix}:{restaurantIdKey}";
        }
    }
}
