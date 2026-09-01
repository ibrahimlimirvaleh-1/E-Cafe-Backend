using ECafe.Domain.Entities;

namespace ECafe.Shared.Services.Jwt.Abstract
{
    public interface IJwtService
    {
        public string GenerateToken(User user, string? fileUrl = null, string? sessionId = null);

        public string GenerateRefreshToken();
    }
}
