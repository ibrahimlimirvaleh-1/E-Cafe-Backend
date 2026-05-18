using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECafe.Application.Services.Jwt.Concrete
{
    public class JwtManager : BaseManager, IJwtService
    {
        private readonly IMinioService _minioService;

        public JwtManager(IHttpContextAccessor httpContextAccessor,
                          IMapper mapper, IConfiguration configuration,
                          IMinioService minioService)
                          : base(httpContextAccessor, mapper, configuration)
        {
            _minioService = minioService;
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

            if (user.RoleId != 1 && user.RoleId != 5)
                claims.Add(new Claim("restaurantId", user.UserRestaurant?.Restaurant.Id.ToString() ?? string.Empty));

            claims.Add(new Claim(ClaimTypes.Role, user.RoleId.ToString()));
            claims.Add(new Claim("roleName", EnumExtensions.GetDescription((RoleCode)user.RoleId)));

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


        public async Task<AuthResponseDto> CreateTokenResponseAsync(Domain.Entities.User user)
        {
            string? fileUrl = null;

            if (user.File != null)
                fileUrl = await _minioService.GenerateFileUrl(user.File.Token);

            return new AuthResponseDto
            {
                AccessToken = GenerateToken(user, fileUrl),
                RefreshToken = GenerateRefreshToken(user)
            };
        }

    }
}
