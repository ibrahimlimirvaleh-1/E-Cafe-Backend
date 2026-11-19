using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

/// <summary>Restoran aidiyyəti qərarında köhnə Redis rolunu istifadə etmir; mövcud çağırış müqaviləsini saxlayır.</summary>
public sealed class UserRestaurantAccessCache : IUserRestaurantAccessCache
{
    private readonly IUserRestaurantRepository _repository;
    private readonly AuthorizationCacheInvalidator _invalidator;

    /// <summary>DI imzasını qoruyur; TTL ayarı çıxışı verilənlər bazasından gec ləğv edə bilməz.</summary>
    public UserRestaurantAccessCache(IDistributedCache cache, IUserRestaurantRepository userRestaurantRepository,
        IConfiguration configuration, ILogger<UserRestaurantAccessCache> logger)
    {
        _repository = userRestaurantRepository;
        _invalidator = new(cache, logger);
    }

    /// <summary>İstifadəçi, restoran və aidiyyətin cari aktivliyini bazadan yoxlayır; səhv alınarsa keşə qayıtmır.</summary>
    public Task<int?> GetActiveRoleIdAsync(int userId, int restaurantId)
        => _repository.GetActiveRoleIdAsync(userId, restaurantId);

    /// <summary>Köhnə açarı yalnız aid olduğu istifadəçi və restoran üçün təmizləyir; oxuma qərarı bundan asılı deyil.</summary>
    public Task InvalidateAsync(int userId, int restaurantId)
        => _invalidator.InvalidateAsync($"user-restaurant-access:{userId}:{restaurantId}");
}
