using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
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
            var claims = new List<Claim>
                {
                    new Claim("userId", user.Id.ToString()),
                    new Claim("name", user.Name),
                    new Claim("surname", user.Surname),
                    new Claim("email", user.Email),
                    new Claim("isActive", user.IsActive.ToString()),
                    new Claim("sessionVersion", user.SessionVersion.ToString()),
                };

            if (!string.IsNullOrWhiteSpace(sessionId))
                claims.Add(new Claim("sessionId", sessionId));

            if (fileUrl != null)
                claims.Add(new Claim("fileUrl", fileUrl));

            var assignedRestaurantId = user.UserRestaurant is { IsActive: true }
                ? user.UserRestaurant.RestaurantId
                : 0;

            if (RequiresActiveRestaurantAssignment(user.RoleId))
            {
                if (assignedRestaurantId <= 0)
                    throw new BusinessRuleException("Restaurant-scoped role requires an active restaurant assignment.");
            }

            if (assignedRestaurantId > 0)
                claims.Add(new Claim("restaurantId", assignedRestaurantId.ToString()));

            claims.Add(new Claim(ClaimTypes.Role, user.RoleId.ToString()));
            claims.Add(new Claim("roleName", EnumExtensions.GetDescription((RoleCode)user.RoleId)));

            foreach (var permission in GetPermissionClaims(user))
                claims.Add(new Claim("permission", permission));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenLifetimeMinutes()),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool RequiresActiveRestaurantAssignment(int roleId)
            => roleId is (int)RoleCode.Manager or
                (int)RoleCode.Waiter or
                (int)RoleCode.Kitchen;

        private int GetAccessTokenLifetimeMinutes()
        {
            var configuredLifetime = _configuration["Jwt:AccessTokenLifetimeMinutes"];
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
