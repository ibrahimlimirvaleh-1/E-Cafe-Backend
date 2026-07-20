using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ECafe.Infrastructure.Authorization
{
    public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options) { }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith($"{RequireActiveRestaurantContractAttribute.PolicyPrefix}:"))
            {
                var restaurantIdKey = policyName.Replace($"{RequireActiveRestaurantContractAttribute.PolicyPrefix}:", "");

                return Task.FromResult<AuthorizationPolicy?>(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddRequirements(new ActiveRestaurantContractRequirement(restaurantIdKey))
                        .Build());
            }

            if (policyName.StartsWith("Permission:"))
            {
                var idPart = policyName.Replace("Permission:", "");

                if (int.TryParse(idPart, out var permissionId))
                {
                    return Task.FromResult<AuthorizationPolicy?>(
                        new AuthorizationPolicyBuilder()
                            .RequireAuthenticatedUser()
                            .AddRequirements(new PermissionRequirement(permissionId))
                            .Build());
                }
            }

            return base.GetPolicyAsync(policyName);
        }
    }
}
