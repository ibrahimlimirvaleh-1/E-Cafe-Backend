using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECafe.Application.Services.Jwt.Concrete
{
    public class JwtManager : BaseManager, IJwtService
    {
        public JwtManager(IHttpContextAccessor httpContextAccessor,
                          IMapper mapper, IConfiguration configuration)
                          : base(httpContextAccessor, mapper, configuration)
        {
        }



        public string GenerateRefreshToken(Domain.Entities.User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: [new Claim("userId", user.Id.ToString())],
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateToken(Domain.Entities.User user)
        {
            var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
        {
            new Claim("userId", user.Id.ToString()),
            new Claim("name", user.Name),
            new Claim("surname", user.Surname),
            new Claim("email", user.Email),
            new Claim("isActive", user.IsActive.ToString()),
            new Claim("restaurantId", user.UserRestaurant?.RestaurantId.ToString() ?? string.Empty)
        };

            if (user.File != null)
            {
                claims.Add(new Claim("fileUrl", user.File.Url));
            }
            if (user.RoleId != 1)
            {
                claims.Add(new Claim("restaurantId", user.UserRestaurant?.Restaurant.Id.ToString() ?? string.Empty));
            }

            claims.Add(new Claim(ClaimTypes.Role, user.RoleId.ToString()));
            claims.Add(new Claim("roleName", user.Role.Name));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public async Task<AuthResponseDto> CreateTokenResponseAsync(Domain.Entities.User user)
        {
            var accessToken = GenerateToken(user);
            var refreshToken = GenerateRefreshToken(user);

            return await Task.FromResult(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }




    }
}
