using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ECafe.Infrastructure.Authorization
{
    public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private const string PolicyPrefix = "Permission:";

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
        }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var permissionValue = policyName.Substring(PolicyPrefix.Length);

                if (int.TryParse(permissionValue, out var permissionId))
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permissionId))
                        .Build();

                    return Task.FromResult<AuthorizationPolicy?>(policy);
                }
            }

            return base.GetPolicyAsync(policyName);
        }
    }
}