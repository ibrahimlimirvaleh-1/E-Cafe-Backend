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

        public string GenerateToken(Domain.Entities.User user, string? fileUrl = null)
        {
            var claims = new List<Claim>
                {
                    new Claim("userId", user.Id.ToString()),
                    new Claim("name", user.Name),
                    new Claim("surname", user.Surname),
                    new Claim("email", user.Email),
                    new Claim("isActive", user.IsActive.ToString()),
                };

            if (fileUrl != null)
                claims.Add(new Claim("fileUrl", fileUrl));

            var assignedRestaurantId = user.UserRestaurant is { RestaurantId: > 0 }
                ? user.UserRestaurant.RestaurantId
                : (int?)null;

            if (RequiresActiveRestaurantAssignment(user.RoleId))
            {
                if (user.UserRestaurant is not { IsActive: true, RestaurantId: > 0 })
                    throw new BusinessRuleException("Restaurant-scoped role requires an active restaurant assignment.");
            }

            if (assignedRestaurantId.HasValue)
                claims.Add(new Claim("restaurantId", assignedRestaurantId.Value.ToString()));

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
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool RequiresActiveRestaurantAssignment(int roleId)
            => roleId is (int)RoleCode.Manager or
                (int)RoleCode.Waiter or
                (int)RoleCode.Kitchen;

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
