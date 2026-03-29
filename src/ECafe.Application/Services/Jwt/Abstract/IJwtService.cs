using ECafe.Domain.Entities;

namespace ECafe.Shared.Services.Jwt.Abstract
{
    public interface IJwtService
    {
        public string GenerateToken(User user);
    }
}
