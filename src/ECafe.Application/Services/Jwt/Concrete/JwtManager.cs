using System.IdentityModel.Tokens.Jwt;
using ECafe.Shared.Security;
using ECafe.Application.Common.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Shared.Extensions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECafe.Application.Services.Jwt.Concrete
{
    public class JwtManager : BaseManager, IJwtService
    {
        public JwtManager(IHttpContextAccessor httpContextAccessor,
                          IMapper mapper,
                          IConfiguration configuration)
                          : base(httpContextAccessor, mapper, configuration)
        {
        }

        public string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        public string GenerateToken(Domain.Entities.User user, string? fileUrl = null, string? sessionId = null)
        {
            UserProfileAccessPolicy.EnsureHasRequiredStaffProfile(user);
            var claims = new List<Claim>
                {
                    new Claim(AuthenticationClaimTypes.UserId, user.Id.ToString()),
                    new Claim(AuthenticationClaimTypes.Name, user.Name),
                    new Claim(AuthenticationClaimTypes.Surname, user.Surname),
                    new Claim(AuthenticationClaimTypes.Email, user.Email),
                    new Claim(AuthenticationClaimTypes.IsActive, user.IsActive.ToString()),
                    new Claim(AuthenticationClaimTypes.SessionVersion, user.SessionVersion.ToString()),
                };

            if (!string.IsNullOrWhiteSpace(sessionId))
                claims.Add(new Claim(AuthenticationClaimTypes.SessionId, sessionId));

            if (fileUrl != null)
                claims.Add(new Claim(AuthenticationClaimTypes.FileUrl, fileUrl));

            var assignedRestaurantIds = user.UserRestaurants
                .Where(userRestaurant => userRestaurant.IsActive)
                .Select(userRestaurant => userRestaurant.RestaurantId)
                .Distinct()
                .OrderBy(restaurantId => restaurantId)
                .ToList();


            if (assignedRestaurantIds.Count > 0)
            {
                claims.Add(new Claim(AuthenticationClaimTypes.RestaurantId, assignedRestaurantIds[0].ToString()));
                claims.Add(new Claim(AuthenticationClaimTypes.RestaurantIds, string.Join(",", assignedRestaurantIds)));

                var restaurantRoles = user.UserRestaurants
                    .Where(userRestaurant => userRestaurant.IsActive)
                    .GroupBy(userRestaurant => userRestaurant.RestaurantId)
                    .Select(group => $"{group.Key}:{group.First().RoleId}")
                    .OrderBy(value => value)
                    .ToList();

                claims.Add(new Claim(AuthenticationClaimTypes.RestaurantRoles, string.Join(",", restaurantRoles)));
            }

            claims.Add(new Claim(ClaimTypes.Role, user.RoleId.ToString()));
            claims.Add(new Claim(AuthenticationClaimTypes.RoleName, EnumExtensions.GetDescription((RoleCode)user.RoleId)));

            foreach (var permission in GetPermissionClaims(user))
                claims.Add(new Claim(AuthenticationClaimTypes.Permission, permission));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration[AuthenticationConfigurationKeys.Jwt.Key]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration[AuthenticationConfigurationKeys.Jwt.Issuer],
                audience: _configuration[AuthenticationConfigurationKeys.Jwt.Audience],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenLifetimeMinutes()),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Mövcud Jwt ayarını oxuyur; əvvəlki 15 dəqiqə fallback-i və 1–60 dəqiqə həddi dəyişmir.</summary>
        private int GetAccessTokenLifetimeMinutes()
        {
            var configuredLifetime = _configuration[AuthenticationConfigurationKeys.Jwt.AccessTokenLifetimeMinutes];
            var lifetimeMinutes = int.TryParse(configuredLifetime, out var parsedLifetime) ? parsedLifetime : 15;
            return Math.Clamp(lifetimeMinutes, 1, 60);
        }

        private static IEnumerable<string> GetPermissionClaims(Domain.Entities.User user)
        {
            return user.Role.RolePermissions
                .Where(rolePermission => !rolePermission.IsDeleted &&
                                         !rolePermission.Permission.IsDeleted &&
                                         Enum.IsDefined(typeof(PermissionCode), rolePermission.PermissionId))
                .Select(rolePermission => ((PermissionCode)rolePermission.PermissionId).ToString())
                .Distinct()
                .OrderBy(permission => permission);
        }
    }
}
