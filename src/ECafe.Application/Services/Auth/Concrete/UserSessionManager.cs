using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete;

public sealed class UserSessionManager : BaseManager, IUserSessionService
{
    private readonly IUserRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserSessionStateCache _userSessionStateCache;
    private readonly UserSessionDeviceLabelOptions _deviceLabelOptions;

    public UserSessionManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        IUserRefreshTokenRepository refreshTokenRepository,
        IUserSessionStateCache userSessionStateCache)
        : base(httpContextAccessor, mapper, configuration)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userSessionStateCache = userSessionStateCache;
        _deviceLabelOptions = LoadDeviceLabelOptions(configuration);
    }

    public async Task<List<UserSessionResponseDto>> GetMySessionsAsync()
    {
        var userId = GetCurrentUserId();
        var currentSessionId = GetCurrentSessionId();
        var tokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, DateTime.UtcNow);

        return tokens
            .Where(token => !string.IsNullOrWhiteSpace(token.SessionId))
            .GroupBy(token => token.SessionId)
            .Select(group =>
            {
                var latestToken = group
                    .OrderByDescending(token => token.CreatedAt)
                    .First();

                return new UserSessionResponseDto
                {
                    SessionId = group.Key,
                    Device = BuildDeviceLabel(latestToken.UserAgent),
                    IpAddress = latestToken.CreatedByIp,
                    CreatedAt = group.Min(token => token.CreatedAt),
                    LastSeenAt = latestToken.CreatedAt,
                    ExpiresAt = latestToken.ExpiresAt,
                    IsCurrent = currentSessionId == group.Key
                };
            })
            .OrderByDescending(session => session.IsCurrent)
            .ThenByDescending(session => session.LastSeenAt)
            .ToList();
    }

    public async Task RevokeMySessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new BusinessRuleException("Session ID is required.");

        var userId = GetCurrentUserId();
        var normalizedSessionId = sessionId.Trim();
        var tokens = await _refreshTokenRepository.GetActiveByUserSessionTrackedAsync(
            userId,
            normalizedSessionId,
            DateTime.UtcNow);

        if (tokens.Count == 0)
            return;

        var revokedAt = DateTime.UtcNow;
        var revokedByIp = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
            token.RevokedByIp = revokedByIp;
        }

        await _refreshTokenRepository.SaveChangesAsync();
        await _userSessionStateCache.InvalidateSessionAsync(userId, normalizedSessionId);
    }

    private string BuildDeviceLabel(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return _deviceLabelOptions.UnknownDeviceLabel ?? string.Empty;

        var browser = ResolveLabel(
            userAgent,
            _deviceLabelOptions.Browsers,
            _deviceLabelOptions.UnknownBrowserLabel);

        var platform = ResolveLabel(
            userAgent,
            _deviceLabelOptions.Platforms,
            _deviceLabelOptions.UnknownPlatformLabel);

        return string.Join(" / ", new[] { browser, platform }.Where(label => !string.IsNullOrWhiteSpace(label)));
    }

    private static string? ResolveLabel(
        string userAgent,
        IEnumerable<UserAgentLabelPattern> patterns,
        string? fallbackLabel)
    {
        return patterns
            .FirstOrDefault(pattern =>
                !string.IsNullOrWhiteSpace(pattern.Pattern) &&
                userAgent.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase))
            ?.Label
            ?? fallbackLabel;
    }

    private static UserSessionDeviceLabelOptions LoadDeviceLabelOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("Auth:SessionDeviceLabel");

        return new UserSessionDeviceLabelOptions
        {
            UnknownDeviceLabel = section["UnknownDeviceLabel"],
            UnknownBrowserLabel = section["UnknownBrowserLabel"],
            UnknownPlatformLabel = section["UnknownPlatformLabel"],
            Browsers = LoadPatterns(section.GetSection("Browsers")),
            Platforms = LoadPatterns(section.GetSection("Platforms"))
        };
    }

    private static List<UserAgentLabelPattern> LoadPatterns(IConfigurationSection section)
    {
        return section
            .GetChildren()
            .Select(child => new UserAgentLabelPattern
            {
                Pattern = child["Pattern"],
                Label = child["Label"]
            })
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern.Pattern) &&
                              !string.IsNullOrWhiteSpace(pattern.Label))
            .ToList();
    }
}
