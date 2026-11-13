using ECafe.Api.Realtime.Connections;
using ECafe.Api.Realtime.Connections.Concrete;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECafe.Tests;

public sealed class RealtimeRedisFactAttribute : FactAttribute
{
    /// <summary>Real Redis testi yalnız ayrıca lokal test serveri açıq şəkildə verildikdə işləyir.</summary>
    public RealtimeRedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS")))
            Skip = "Set ECAFE_TEST_REDIS to an isolated Redis test server.";
    }
}

public sealed class RealtimeConnectionRedisTests
{
    [RealtimeRedisFact]
    public async Task LuaAdmissionEnforcesOneLimitAcrossIndependentApiInstances()
    {
        await using var first = Store(limit: 3);
        await using var second = Store(limit: 3);
        var userId = Random.Shared.Next(1000000, int.MaxValue);
        var attempts = Enumerable.Range(0, 32).Select(index => (Store: index % 2 == 0 ? first : second, Id: Guid.NewGuid().ToString("N"))).ToArray();
        try
        {
            var results = await Task.WhenAll(attempts.Select(attempt => attempt.Store.TryAcquireAsync(userId, attempt.Id)));
            Assert.Equal(3, results.Count(value => value));
            var winner = attempts[Array.FindIndex(results, value => value)];
            Assert.True(await second.TryAcquireAsync(userId, winner.Id));
            Assert.False(await second.TryAcquireAsync(userId, "extra"));
            await first.ReleaseAsync(userId, winner.Id);
            await first.ReleaseAsync(userId, winner.Id);
            Assert.True(await second.TryAcquireAsync(userId, "replacement"));
            Assert.False(await first.TryAcquireAsync(userId, "one-too-many"));
        }
        finally
        {
            foreach (var attempt in attempts) await first.ReleaseAsync(userId, attempt.Id);
            await second.ReleaseAsync(userId, "replacement");
        }
    }

    [RealtimeRedisFact]
    public async Task RedisClockReclaimsOrphansAndOldDisconnectCannotDeleteTheNewReservation()
    {
        await using var store = Store(limit: 1, leaseSeconds: 1);
        var userId = Random.Shared.Next(1000000, int.MaxValue);
        try
        {
            Assert.True(await store.TryAcquireAsync(userId, "orphan"));
            Assert.False(await store.TryAcquireAsync(userId, "blocked"));
            await Task.Delay(1400);
            Assert.False(await store.RenewAsync(userId, "orphan"));
            Assert.True(await store.TryAcquireAsync(userId, "new"));
            await store.ReleaseAsync(userId, "orphan");
            Assert.False(await store.TryAcquireAsync(userId, "extra"));
        }
        finally { await store.ReleaseAsync(userId, "new"); }
    }

    [RealtimeRedisFact]
    public async Task RenewedRedisReservationStaysCountedBeyondTheOriginalTtl()
    {
        await using var store = Store(limit: 1, leaseSeconds: 2);
        var userId = Random.Shared.Next(1000000, int.MaxValue);
        try
        {
            Assert.True(await store.TryAcquireAsync(userId, "active"));
            await Task.Delay(1200);
            Assert.True(await store.RenewAsync(userId, "active"));
            await Task.Delay(1200);
            Assert.False(await store.TryAcquireAsync(userId, "extra"));
        }
        finally { await store.ReleaseAsync(userId, "active"); }
    }

    /// <summary>Layihənin Redis-inə deyil, yalnız ECAFE_TEST_REDIS ilə verilmiş ayrıca test serverinə qoşulur.</summary>
    private static RedisRealtimeConnectionLeaseStore Store(int limit, int leaseSeconds = 90)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["SignalR:Redis:Connection"] = Environment.GetEnvironmentVariable("ECAFE_TEST_REDIS") }).Build();
        return new RedisRealtimeConnectionLeaseStore(configuration, Options.Create(new RealtimeConnectionOptions { MaxConnectionsPerUser = limit, LeaseSeconds = leaseSeconds }));
    }
}
