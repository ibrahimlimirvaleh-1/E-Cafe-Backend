using ECafe.Application.DTOs.Auth;
using ECafe.Domain.Entities;

namespace ECafe.Shared.Services.Jwt.Abstract
{
    public interface IJwtService
    {
        public string GenerateToken(User user, string? fileUrl = null);

        public string GenerateRefreshToken(User user);

        public Task<AuthResponseDto> CreateTokenResponseAsync(User user);

    }
}
