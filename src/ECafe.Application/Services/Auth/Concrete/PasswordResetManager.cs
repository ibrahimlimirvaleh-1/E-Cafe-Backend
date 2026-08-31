using System.Security.Cryptography;
using System.Text;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.PasswordResetToken;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Services.Auth.Concrete;

public class PasswordResetManager : IPasswordResetService
{
    private const int TokenLifetimeMinutes = 30;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUserRefreshTokenRepository _refreshTokenRepository;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetManager> _logger;
    private readonly IUserSessionStateCache _userSessionStateCache;

    public PasswordResetManager(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUserRefreshTokenRepository refreshTokenRepository,
        IEmailOutboxService emailOutboxService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<PasswordResetManager> logger,
        IUserSessionStateCache userSessionStateCache)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _emailOutboxService = emailOutboxService;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
        _userSessionStateCache = userSessionStateCache;
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailTrackedAsync(normalizedEmail);

        if (user is null)
        {
            _logger.LogInformation("Password reset requested for a non-existing email.");
            return;
        }

        if (!user.IsActive)
        {
            _logger.LogInformation("Password reset requested for inactive user {UserId}.", user.Id);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var activeTokens = await _passwordResetTokenRepository.GetActiveByUserIdTrackedAsync(user.Id, nowUtc);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = nowUtc;
        }

        var plainToken = GenerateSecureToken();
        var resetToken = new Domain.Entities.PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(plainToken),
            ExpiresAt = nowUtc.AddMinutes(TokenLifetimeMinutes),
            CreatedByIp = GetRequestIp(),
            UserAgent = GetUserAgent()
        };

        await _passwordResetTokenRepository.Add(resetToken);
        await _passwordResetTokenRepository.SaveChangesAsync();

        await EnqueueResetLinkEmailAsync(user, BuildResetUrl(plainToken));
        _logger.LogInformation("Password reset email outbox event queued for user {UserId}.", user.Id);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var nowUtc = DateTime.UtcNow;
        var tokenHash = HashToken(request.Token);
        var resetToken = await _passwordResetTokenRepository.GetActiveByTokenHashTrackedAsync(tokenHash, nowUtc);

        if (resetToken is null)
            throw new BusinessRuleException(ErrorCode.PasswordResetTokenInvalidOrExpired);

        if (!resetToken.User.IsActive)
            throw new BusinessRuleException(ErrorCode.UserDeactivated);

        if (!string.IsNullOrWhiteSpace(resetToken.User.Password) &&
            BCrypt.Net.BCrypt.Verify(request.Password, resetToken.User.Password))
        {
            throw new BusinessRuleException(ErrorCode.PasswordResetNewPasswordMustBeDifferent);
        }

        resetToken.User.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
        resetToken.User.PasswordSetAt = nowUtc;
        resetToken.User.SessionVersion++;
        resetToken.UsedAt = nowUtc;

        await RevokeActiveRefreshTokensAsync(resetToken.UserId, nowUtc);
        await _passwordResetTokenRepository.SaveChangesAsync();
        await _userSessionStateCache.InvalidateAsync(resetToken.UserId);

        await EnqueuePasswordChangedEmailAsync(resetToken.User);
    }

    private async Task RevokeActiveRefreshTokensAsync(int userId, DateTime nowUtc)
    {
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdTrackedAsync(userId, nowUtc);

        foreach (var token in activeTokens)
        {
            token.RevokedAt ??= nowUtc;
            token.RevokedByIp ??= GetRequestIp();
        }
    }

    private Task EnqueueResetLinkEmailAsync(Domain.Entities.User user, string resetUrl)
    {
        var body = $"""
        Salam {user.Name},

        ECafe hesabiniz ucun sifre yenileme sorgusu alindi.

        Sifrenizi yenilemek ucun asagidaki linke kecin:
        {resetUrl}

        Link {TokenLifetimeMinutes} deqiqe aktivdir ve yalniz bir defe istifade oluna biler.

        Eger bu emeliyyati siz etmemisinizse, bu emaili nezere almayin.
        """;

        return _emailOutboxService.EnqueueEmailAsync(
            user.Email,
            $"{user.Name} {user.Surname}",
            "ECafe sifre yenileme linki",
            body,
            OutboxAggregateTypes.User,
            user.Id,
            AuditEntityTypes.User,
            user.Id);
    }

    private Task EnqueuePasswordChangedEmailAsync(Domain.Entities.User user)
    {
        var ipAddress = GetRequestIp() ?? "unknown";
        var userAgent = GetUserAgent() ?? "unknown";
        var body = $"""
        Salam {user.Name},

        ECafe hesabinizin sifresi ugurla yenilendi.

        IP address: {ipAddress}
        Cihaz/browser: {userAgent}

        Bu emeliyyati siz etmemisinizse, derhal yeni sifre yenileme sorgusu yaradib hesab tehlukesizliyini yoxlayin.
        """;

        return _emailOutboxService.EnqueueEmailAsync(
            user.Email,
            $"{user.Name} {user.Surname}",
            "ECafe hesabinizin sifresi yenilendi",
            body,
            OutboxAggregateTypes.User,
            user.Id,
            AuditEntityTypes.User,
            user.Id);
    }

    private string BuildResetUrl(string token)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            frontendBaseUrl = "http://localhost:5173";

        return $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
    }

    private string? GetRequestIp()
        => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
        => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

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
