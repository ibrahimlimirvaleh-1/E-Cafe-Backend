using System.Text.Json;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

public sealed class UserSessionStateCache : IUserSessionStateCache
{
    private const int DefaultCacheMinutes = 5;

    private readonly IDistributedCache _cache;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserSessionStateCache> _logger;
    private readonly TimeSpan _cacheLifetime;

    public UserSessionStateCache(
        IDistributedCache cache,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<UserSessionStateCache> logger)
    {
        _cache = cache;
        _userRepository = userRepository;
        _logger = logger;
        _cacheLifetime = TimeSpan.FromMinutes(GetCacheLifetimeMinutes(configuration));
    }

    public async Task<UserSessionState?> GetAsync(int userId)
    {
        var cacheKey = Key(userId);

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached))
                return JsonSerializer.Deserialize<UserSessionState>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User session state cache read failed for user {UserId}. Falling back to database.", userId);
        }

        var sessionState = await _userRepository.GetSessionStateAsync(userId);
        if (sessionState is null)
            return null;

        var value = new UserSessionState(sessionState.Value.IsActive, sessionState.Value.SessionVersion);

        try
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(value),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheLifetime
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User session state cache write failed for user {UserId}.", userId);
        }

        return value;
    }

    public async Task InvalidateAsync(int userId)
    {
        try
        {
            await _cache.RemoveAsync(Key(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User session state cache invalidation failed for user {UserId}.", userId);
        }
    }

    private static string Key(int userId) => $"user-session-state:{userId}";

    private static int GetCacheLifetimeMinutes(IConfiguration configuration)
    {
        var configured = configuration["Auth:SessionStateCacheMinutes"];
        return int.TryParse(configured, out var minutes)
            ? Math.Clamp(minutes, 1, 60)
            : DefaultCacheMinutes;
    }
}
