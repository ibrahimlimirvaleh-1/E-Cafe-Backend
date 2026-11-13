using ECafe.Api.Realtime.Connections.Abstract;
using Microsoft.Extensions.Options;

namespace ECafe.Api.Realtime.Connections.Concrete;

public sealed class MemoryRealtimeConnectionLeaseStore(IOptions<RealtimeConnectionOptions> options, TimeProvider clock)
    : IRealtimeConnectionLeaseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Dictionary<string, DateTimeOffset>> _users = [];

    /// <summary>Tək API nüsxəsində eyni hesabın paralel bağlantılarını atomik sayır; vaxtı bitmiş rezervləri əvvəl təmizləyir.</summary>
    public Task<bool> TryAcquireAsync(int userId, string leaseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (!_users.TryGetValue(userId, out var leases)) _users[userId] = leases = [];
            RemoveExpired(leases, now);
            if (!leases.ContainsKey(leaseId) && leases.Count >= options.Value.MaxConnectionsPerUser)
                return Task.FromResult(false);
            leases[leaseId] = now.AddSeconds(options.Value.LeaseSeconds);
            return Task.FromResult(true);
        }
    }

    /// <summary>Yalnız hələ etibarlı rezervin ömrünü uzadır; itirilmiş rezervi yenidən yaratmır.</summary>
    public Task<bool> RenewAsync(int userId, string leaseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (!_users.TryGetValue(userId, out var leases)) return Task.FromResult(false);
            RemoveExpired(leases, now);
            if (!leases.ContainsKey(leaseId))
            {
                if (leases.Count == 0) _users.Remove(userId);
                return Task.FromResult(false);
            }
            leases[leaseId] = now.AddSeconds(options.Value.LeaseSeconds);
            return Task.FromResult(true);
        }
    }

    /// <summary>Bağlanan bağlantının rezervini bir dəfə silir; başqa tabın və ya hesabın rezervinə toxunmur.</summary>
    public Task ReleaseAsync(int userId, string leaseId)
    {
        lock (_gate)
        {
            if (_users.TryGetValue(userId, out var leases))
            {
                leases.Remove(leaseId);
                if (leases.Count == 0) _users.Remove(userId);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Qəfil kəsilmədən qalan rezervləri saatın göstərdiyi son tarixə görə çıxarır.</summary>
    private static void RemoveExpired(Dictionary<string, DateTimeOffset> leases, DateTimeOffset now)
    {
        foreach (var id in leases.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray()) leases.Remove(id);
    }
}
