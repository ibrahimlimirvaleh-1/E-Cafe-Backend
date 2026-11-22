using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

/// <summary>Mövcud interfeysi saxlayır, lakin sessiya qərarını TTL-li Redis məlumatından deyil, cari bazadan verir.</summary>
public sealed class UserSessionStateCache : IUserSessionStateCache
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRefreshTokenRepository _refreshTokenRepository;
    private readonly AuthorizationCacheInvalidator _invalidator;

    /// <summary>DI müqaviləsini qoruyur; əvvəlki TTL ayarı artıq təhlükəsizlik qərarının müddətini müəyyən etmir.</summary>
    public UserSessionStateCache(IDistributedCache cache, IUserRepository userRepository,
        IUserRefreshTokenRepository refreshTokenRepository, IConfiguration configuration,
        ILogger<UserSessionStateCache> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _invalidator = new(cache, logger);
    }

    /// <summary>Aktivliyi, versiyanı və işçi profilinin mövcudluğunu bazadan oxuyur; köhnə Redis və ya müştəri JWT-si ilə giriş vermir.</summary>
    public async Task<UserSessionState?> GetAsync(int userId)
    {
        var state = await _userRepository.GetSessionStateAsync(userId);
        return state is null ? null : new(state.Value.IsActive, state.Value.SessionVersion, state.Value.HasRequiredStaffProfile);
    }

    /// <summary>Cihaz sessiyasının ləğvini və bitmə vaxtını hər dəfə bazadan yoxlayır; gecikmiş keş yazısı sessiyanı diriltmir.</summary>
    public Task<bool> IsSessionActiveAsync(int userId, string sessionId)
        => _refreshTokenRepository.HasActiveUserSessionAsync(userId, sessionId, DateTime.UtcNow);

    /// <summary>Keçid uyğunluğu üçün köhnə istifadəçi açarını təmizləyir; icazə yoxlaması həmin açarı oxumur.</summary>
    public Task InvalidateAsync(int userId)
        => _invalidator.InvalidateAsync($"user-session-state:{userId}");

    /// <summary>Keçid uyğunluğu üçün yalnız göstərilən cihazın köhnə açarını silir; digər sessiyaları ləğv etmir.</summary>
    public Task InvalidateSessionAsync(int userId, string sessionId)
        => _invalidator.InvalidateAsync($"user-session-active:{userId}:{sessionId}");
}
