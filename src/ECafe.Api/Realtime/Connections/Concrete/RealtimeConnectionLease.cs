using ECafe.Api.Realtime.Connections.Abstract;

namespace ECafe.Api.Realtime.Connections.Concrete;

public sealed class RealtimeConnectionLease : IAsyncDisposable
{
    private readonly IRealtimeConnectionLeaseStore _store;
    private readonly int _userId;
    private readonly string _leaseId;
    private readonly Action _abort;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly RealtimeConnectionOptions _options;
    private readonly CancellationTokenSource _stopping;
    private readonly Task _lifetime;
    private long _lastConfirmed;
    private int _disposed;

    /// <summary>Uğurla ayrılmış rezervi bağlantının kəsilmə siqnalına bağlayır və müddətli yenilənməni başladır.</summary>
    public RealtimeConnectionLease(IRealtimeConnectionLeaseStore store, int userId, string leaseId,
        Action abort, CancellationToken connectionAborted, RealtimeConnectionOptions options,
        TimeProvider clock, ILogger logger, long acquiredAt)
    {
        _store = store;
        _userId = userId;
        _leaseId = leaseId;
        _abort = abort;
        _clock = clock;
        _logger = logger;
        _options = options;
        _lastConfirmed = acquiredAt;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(connectionAborted);
        _lifetime = RunAsync();
    }

    /// <summary>Bağlantı yaşadıqca rezervi yeniləyir; Redis qısa müddət əlçatan olmadıqda TTL daxilində bərpanı gözləyir.</summary>
    private async Task RunAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.RenewalSeconds), _clock);
            while (await timer.WaitForNextTickAsync(_stopping.Token))
            {
                var started = _clock.GetTimestamp();
                try
                {
                    if (!await _store.RenewAsync(_userId, _leaseId, _stopping.Token))
                    {
                        _abort();
                        return;
                    }
                    _lastConfirmed = started;
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    _logger.LogWarning("Realtime lease renewal failed ({ErrorType}).", error.GetType().Name);
                    if (_clock.GetElapsedTime(_lastConfirmed) >= TimeSpan.FromSeconds(_options.LeaseSeconds - _options.RenewalSeconds))
                    {
                        _abort();
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        finally
        {
            try { await _store.ReleaseAsync(_userId, _leaseId); }
            catch (Exception error)
            {
                _logger.LogWarning("Realtime lease release failed; TTL will reclaim it ({ErrorType}).", error.GetType().Name);
            }
        }
    }

    /// <summary>Disconnect və uğursuz hub açılışında rezervi dərhal buraxır; təkrar dispose eyni rezervi yenidən silmir.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                await _stopping.CancelAsync();
                await _lifetime;
            }
            finally { _stopping.Dispose(); }
        }
        else await _lifetime;
    }
}
