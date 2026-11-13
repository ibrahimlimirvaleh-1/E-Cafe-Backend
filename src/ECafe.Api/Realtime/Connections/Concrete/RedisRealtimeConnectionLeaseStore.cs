using System.Globalization;
using ECafe.Api.Realtime.Connections.Abstract;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ECafe.Api.Realtime.Connections.Concrete;

public sealed class RedisRealtimeConnectionLeaseStore : IRealtimeConnectionLeaseStore, IAsyncDisposable
{
    private const string AcquireScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
        if not redis.call('ZSCORE', KEYS[1], ARGV[1]) and redis.call('ZCARD', KEYS[1]) >= tonumber(ARGV[2]) then return 0 end
        redis.call('ZADD', KEYS[1], now + tonumber(ARGV[3]), ARGV[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[3])
        return 1
        """;
    private const string RenewScript = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
        if not redis.call('ZSCORE', KEYS[1], ARGV[1]) then return 0 end
        redis.call('ZADD', KEYS[1], now + tonumber(ARGV[2]), ARGV[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[2])
        return 1
        """;
    private const string ReleaseScript = """
        redis.call('ZREM', KEYS[1], ARGV[1])
        if redis.call('ZCARD', KEYS[1]) == 0 then redis.call('DEL', KEYS[1]) end
        return 1
        """;

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly string _connectionString;
    private ConnectionMultiplexer? _connection;
    private bool _disposed;
    private readonly RealtimeConnectionOptions _options;

    /// <summary>Backplane-in eyni Redis ünvanını seçir; məxfi qoşulma məlumatını loga çıxarmır və ayrıca bağlantını tənbəl yaradır.</summary>
    public RedisRealtimeConnectionLeaseStore(IConfiguration configuration, IOptions<RealtimeConnectionOptions> options)
    {
        _options = options.Value;
        var connection = configuration["SignalR:Redis:Connection"] ?? configuration["Redis:Connection"];
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("SignalR Redis connection is required when the Redis backplane is enabled.");
        _connectionString = connection;
    }

    /// <summary>Redis saatı və bir Lua əməliyyatı ilə bütün API nüsxələri üzrə ortaq limiti qoruyur.</summary>
    public async Task<bool> TryAcquireAsync(int userId, string leaseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync();
        return (long)await database.ScriptEvaluateAsync(AcquireScript, [Key(userId)],
            [leaseId, _options.MaxConnectionsPerUser, _options.LeaseSeconds * 1000L]) == 1;
    }

    /// <summary>Redis-də mövcud və vaxtı bitməmiş rezervi uzadır; başqa bağlantının boşaltdığı yeri yenidən mənimsəmir.</summary>
    public async Task<bool> RenewAsync(int userId, string leaseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync();
        return (long)await database.ScriptEvaluateAsync(RenewScript, [Key(userId)],
            [leaseId, _options.LeaseSeconds * 1000L]) == 1;
    }

    /// <summary>Yalnız serverin yaratdığı rezervi silir; təkrar disconnect başqa bağlantının sayını azaltmır.</summary>
    public async Task ReleaseAsync(int userId, string leaseId)
    {
        var database = await GetDatabaseAsync();
        await database.ScriptEvaluateAsync(ReleaseScript, [Key(userId)], [leaseId]);
    }

    /// <summary>Yalnız bu qorumanın ayrıca namespace-ində, Redis Cluster üçün eyni hesab hash slot-unda açar qurur.</summary>
    private static RedisKey Key(int userId) => "ecafe:realtime:connections:{" + userId.ToString(CultureInfo.InvariantCulture) + "}";

    /// <summary>Tənbəl yaradılan ortaq bağlantını qaytarır; timeout olmuş ilk qoşulmanın sonrakı cəhddə bərpasına imkan verir.</summary>
    private async Task<IDatabase> GetDatabaseAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connection is { } existing) return existing.GetDatabase();
        await _connectionLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _connection ??= await ConnectAsync(_connectionString);
            return _connection.GetDatabase();
        }
        finally { _connectionLock.Release(); }
    }

    /// <summary>Qoruma sorğularını qısa timeout və fail-fast növbə ilə məhdudlaşdırır; offline əmrlərin gecikmiş icrasına yol vermir.</summary>
    private static Task<ConnectionMultiplexer> ConnectAsync(string connection)
    {
        var settings = ConfigurationOptions.Parse(connection);
        settings.ConnectTimeout = 3000;
        settings.AsyncTimeout = 3000;
        settings.ConnectRetry = 0;
        settings.AbortOnConnectFail = false;
        settings.BacklogPolicy = BacklogPolicy.FailFast;
        return ConnectionMultiplexer.ConnectAsync(settings);
    }

    /// <summary>Tətbiq dayananda yalnız bu xidmətin yaratdığı Redis bağlantısını bağlayır.</summary>
    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_connection is not { } connection) return;
            await connection.CloseAsync();
            connection.Dispose();
        }
        finally { _connectionLock.Release(); }
    }
}
