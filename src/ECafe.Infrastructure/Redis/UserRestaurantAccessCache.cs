using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

public sealed class UserRestaurantAccessCache : IUserRestaurantAccessCache
{
    private const int DefaultCacheMinutes = 2;
    private const int NoAccessCacheValue = 0;

    private readonly IDistributedCache _cache;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly ILogger<UserRestaurantAccessCache> _logger;
    private readonly TimeSpan _cacheLifetime;

    public UserRestaurantAccessCache(
        IDistributedCache cache,
        IUserRestaurantRepository userRestaurantRepository,
        IConfiguration configuration,
        ILogger<UserRestaurantAccessCache> logger)
    {
        _cache = cache;
        _userRestaurantRepository = userRestaurantRepository;
        _logger = logger;
        _cacheLifetime = TimeSpan.FromMinutes(GetCacheLifetimeMinutes(configuration));
    }

    public async Task<int?> GetActiveRoleIdAsync(int userId, int restaurantId)
    {
        var cacheKey = Key(userId, restaurantId);

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (int.TryParse(cached, out var cachedRoleId))
                return cachedRoleId > 0 ? cachedRoleId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "User restaurant access cache read failed for user {UserId} and restaurant {RestaurantId}. Falling back to database.",
                userId,
                restaurantId);
        }

        var roleId = await _userRestaurantRepository.GetActiveRoleIdAsync(userId, restaurantId);

        try
        {
            await _cache.SetStringAsync(
                cacheKey,
                (roleId ?? NoAccessCacheValue).ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheLifetime
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "User restaurant access cache write failed for user {UserId} and restaurant {RestaurantId}.",
                userId,
                restaurantId);
        }

        return roleId;
    }

    public async Task InvalidateAsync(int userId, int restaurantId)
    {
        try
        {
            await _cache.RemoveAsync(Key(userId, restaurantId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "User restaurant access cache invalidation failed for user {UserId} and restaurant {RestaurantId}.",
                userId,
                restaurantId);
        }
    }

    private static string Key(int userId, int restaurantId)
        => $"user-restaurant-access:{userId}:{restaurantId}";

    private static int GetCacheLifetimeMinutes(IConfiguration configuration)
    {
        var configured = configuration["Auth:RestaurantAccessCacheMinutes"];
        return int.TryParse(configured, out var minutes)
            ? Math.Clamp(minutes, 1, 15)
            : DefaultCacheMinutes;
    }
}
