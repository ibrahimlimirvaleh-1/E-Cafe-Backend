using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace ECafe.Infrastructure.Services.Localization;

public sealed class TranslationMemoryCache : ITranslationCache, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1 });
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _invalidationLock = new();
    private long _generation;

    /// <summary>Yalnız bir yayımlanmış snapshot saxlayır; paralel soyuq sorğuları bir DB oxunuşunda birləşdirir və 30 saniyə sonra versiyanı yenidən yoxlayır.</summary>
    public async Task<TranslationSnapshot> GetAsync(Func<CancellationToken, Task<TranslationSnapshot>> loader, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<TranslationSnapshot>("published", out var value)) return value!;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue("published", out value)) return value!;
            var generation = Volatile.Read(ref _generation);
            value = await loader(cancellationToken);
            lock (_invalidationLock)
            {
                if (generation == Volatile.Read(ref _generation))
                    _cache.Set("published", value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
            }
            return value;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Yayım commit olduqdan sonra cari prosesdə köhnə lüğəti silir; paralel əvvəlki oxunuşun onu yenidən keşləməsini də dayandırır.</summary>
    public void Invalidate()
    {
        lock (_invalidationLock)
        {
            Interlocked.Increment(ref _generation);
            _cache.Remove("published");
        }
    }
    /// <summary>Servis dayananda ayrılmış keş və kilidin resurslarını azad edir.</summary>
    public void Dispose() { _cache.Dispose(); _gate.Dispose(); }
}
