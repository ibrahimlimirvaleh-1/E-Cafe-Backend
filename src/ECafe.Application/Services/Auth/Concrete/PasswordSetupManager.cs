using System.Security.Cryptography;
using System.Text;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserPasswordSetupToken;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete
{
    public class PasswordSetupManager : IPasswordSetupService
    {
        private const int TokenLifetimeMinutes = 60;

        private readonly IUserRepository _userRepository;
        private readonly IUserPasswordSetupTokenRepository _tokenRepository;
        private readonly IEmailOutboxService _emailOutboxService;
        private readonly IConfiguration _configuration;

        public PasswordSetupManager(
            IUserRepository userRepository,
            IUserPasswordSetupTokenRepository tokenRepository,
            IEmailOutboxService emailOutboxService,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _emailOutboxService = emailOutboxService;
            _configuration = configuration;
        }

        public async Task SendSetupLinkAsync(Domain.Entities.User user)
        {
            var nowUtc = DateTime.UtcNow;
            var activeTokens = await _tokenRepository.GetActiveByUserIdTrackedAsync(user.Id, nowUtc);

            foreach (var token in activeTokens)
            {
                token.UsedAt = nowUtc;
            }

            var plainToken = GenerateSecureToken();
            var setupToken = new Domain.Entities.UserPasswordSetupToken
            {
                UserId = user.Id,
                TokenHash = HashToken(plainToken),
                ExpiresAt = nowUtc.AddMinutes(TokenLifetimeMinutes)
            };

            await _tokenRepository.Add(setupToken);
            await _tokenRepository.SaveChangesAsync();

            var setupUrl = BuildSetupUrl(plainToken);
            var body = $"""
            Salam {user.Name},

            ECafe hesabiniz yaradildi.

            Sifrenizi teyin etmek ucun asagidaki linke kecin:
            {setupUrl}

            Link {TokenLifetimeMinutes} deqiqe aktivdir.

            Eger bu emeliyyati siz gozlemirdinizse, bu emaili nezere almayin.
            """;

            await _emailOutboxService.EnqueueEmailAsync(
                user.Email,
                $"{user.Name} {user.Surname}",
                "ECafe hesabiniz ucun sifre teyin edin",
                body,
                OutboxAggregateTypes.User,
                user.Id,
                AuditEntityTypes.User,
                user.Id);
        }

        public async Task SetPasswordAsync(SetPasswordRequest request)
        {
            var tokenHash = HashToken(request.Token);
            var setupToken = await _tokenRepository.GetActiveByTokenHashTrackedAsync(tokenHash, DateTime.UtcNow);

            if (setupToken is null)
                throw new BusinessRuleException("Password setup link is invalid or expired.");

            var nowUtc = DateTime.UtcNow;
            setupToken.User.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
            setupToken.User.PasswordSetAt = nowUtc;
            setupToken.UsedAt = nowUtc;

            await _userRepository.SaveChangesAsync();
        }

        private string BuildSetupUrl(string token)
        {
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                frontendBaseUrl = "http://localhost:5173";

            return $"{frontendBaseUrl}/set-password?token={Uri.EscapeDataString(token)}";
        }

        private static string GenerateSecureToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", string.Empty);
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
