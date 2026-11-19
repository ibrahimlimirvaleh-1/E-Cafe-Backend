using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Redis;

internal sealed class AuthorizationCacheInvalidator(IDistributedCache cache, ILogger logger)
{
    /// <summary>Köhnə açarı təmizləyir; Redis nasazlığı bazada artıq tamamlanmış təhlükəsizlik dəyişikliyini uğursuz göstərmir.</summary>
    public void Invalidate(string key)
    {
        try { cache.Remove(key); }
        catch (Exception error) when (IsProviderFailure(error)) { Report(error); }
    }

    /// <summary>Köhnə açarı asinxron silir; provayder nasazlığını qeyd edir, sorğunun ləğvini və proqramlaşdırma xətalarını gizlətmir.</summary>
    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { await cache.RemoveAsync(key, cancellationToken); }
        catch (Exception error) when (IsProviderFailure(error)) { Report(error); }
    }

    /// <summary>Keş sərhədində provayder xətalarını ayırır; ləğvetmə, səhv istifadə və resurs çatışmazlığını gizlətmir.</summary>
    private static bool IsProviderFailure(Exception error)
        => error is not (OperationCanceledException or ArgumentException or InvalidOperationException or
            NotSupportedException or OutOfMemoryException or TypeLoadException or MemberAccessException);

    /// <summary>Məxfi provayder mətnini və açarları loqa çıxarmadan təmizləmə nasazlığını müşahidə edilən saxlayır.</summary>
    private void Report(Exception error)
        => logger.LogWarning("Legacy authorization cache eviction unavailable ({ErrorType}); database authorization remains authoritative.",
            error.GetType().Name);
}
