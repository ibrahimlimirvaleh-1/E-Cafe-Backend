using ECafe.Api.Realtime;
using ECafe.Api.Realtime.Connections;
using ECafe.Api.Realtime.Connections.Abstract;
using ECafe.Api.Realtime.Connections.Concrete;
using ECafe.Application.Common.Errors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RealtimeConnectionLimitTests
{
    [Fact]
    public async Task ParallelReservationsNeverExceedThePerUserLimitAndDoNotBlockOtherUsers()
    {
        var store = MemoryStore(5);
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => store.TryAcquireAsync(10, i.ToString()))));
        Assert.Equal(5, results.Count(value => value));
        Assert.True(await store.TryAcquireAsync(20, "other-account"));
    }

    [Fact]
    public async Task ReservationAndReleaseAreIdempotentAndCannotReleaseAnotherAccountOrConnection()
    {
        var store = MemoryStore(2);
        Assert.True(await store.TryAcquireAsync(10, "first"));
        Assert.True(await store.TryAcquireAsync(10, "first"));
        Assert.True(await store.TryAcquireAsync(10, "second"));
        await store.ReleaseAsync(20, "first");
        Assert.False(await store.TryAcquireAsync(10, "third"));
        await store.ReleaseAsync(10, "first");
        await store.ReleaseAsync(10, "first");
        Assert.True(await store.TryAcquireAsync(10, "third"));
        Assert.False(await store.TryAcquireAsync(10, "fourth"));
    }

    [Fact]
    public async Task ExpiredReservationsAreReclaimedAndOldHeartbeatCannotResurrectThem()
    {
        var clock = new ManualClock();
        var store = new MemoryRealtimeConnectionLeaseStore(Options.Create(new RealtimeConnectionOptions { MaxConnectionsPerUser = 1 }), clock);
        Assert.True(await store.TryAcquireAsync(10, "old"));
        clock.Advance(TimeSpan.FromSeconds(91));
        Assert.False(await store.RenewAsync(10, "old"));
        Assert.True(await store.TryAcquireAsync(10, "new"));
        await store.ReleaseAsync(10, "old");
        Assert.False(await store.TryAcquireAsync(10, "extra"));
    }

    [Fact]
    public async Task HeartbeatKeepsActiveReservationBeyondItsOriginalExpiry()
    {
        var clock = new ManualClock();
        var store = new MemoryRealtimeConnectionLeaseStore(Options.Create(new RealtimeConnectionOptions { MaxConnectionsPerUser = 1 }), clock);
        Assert.True(await store.TryAcquireAsync(10, "active"));
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(await store.RenewAsync(10, "active"));
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(await store.TryAcquireAsync(10, "extra"));
    }

    [Fact]
    public async Task FilterRejectsOnlyTheExtraConnectionAndFreesSlotOnDisconnect()
    {
        var store = MemoryStore(1);
        var filter = Filter(store);
        var first = Context("10");
        var second = Context("10");
        await filter.OnConnectedAsync(first, _ => Task.CompletedTask);
        try
        {
            await Assert.ThrowsAsync<HubException>(() => filter.OnConnectedAsync(second, _ => throw new InvalidOperationException("must not enter")));
            Assert.True(await store.TryAcquireAsync(20, "another-user"));
        }
        finally { await filter.OnDisconnectedAsync(first, null, (_, _) => Task.CompletedTask); }
        await filter.OnConnectedAsync(second, _ => Task.CompletedTask);
        await filter.OnDisconnectedAsync(second, null, (_, _) => Task.CompletedTask);
        await filter.OnDisconnectedAsync(second, null, (_, _) => Task.CompletedTask);
        Assert.True(await store.TryAcquireAsync(10, "replacement"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-user")]
    public async Task InvalidIdentityCannotReserveAConnection(string? userId)
    {
        var store = new Mock<IRealtimeConnectionLeaseStore>(MockBehavior.Strict);
        await Assert.ThrowsAsync<HubException>(() => Filter(store.Object).OnConnectedAsync(Context(userId), _ => Task.CompletedTask));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedHubStartupReleasesExactlyOnce()
    {
        var store = SuccessfulStore();
        var filter = Filter(store.Object);
        var context = Context("10");
        await Assert.ThrowsAsync<InvalidOperationException>(() => filter.OnConnectedAsync(context, _ => throw new InvalidOperationException("startup failed")));
        await filter.OnDisconnectedAsync(context, null, (_, _) => Task.CompletedTask);
        store.Verify(x => x.ReleaseAsync(10, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CancellationDuringAdmissionStillReleasesTheAcquiredReservation()
    {
        using var cancellation = new CancellationTokenSource();
        var store = SuccessfulStore();
        store.Setup(x => x.TryAcquireAsync(10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => { cancellation.Cancel(); return Task.FromResult(true); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Filter(store.Object)
            .OnConnectedAsync(Context("10", cancellation.Token), _ => Task.CompletedTask));
        store.Verify(x => x.ReleaseAsync(10, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StoreFailureIsFailClosedAndDoesNotExposeConnectionDetails()
    {
        var store = new Mock<IRealtimeConnectionLeaseStore>();
        store.Setup(x => x.TryAcquireAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("password=never-expose-this"));
        var error = await Assert.ThrowsAsync<HubException>(() => Filter(store.Object).OnConnectedAsync(Context("10"), _ => throw new InvalidOperationException()));
        Assert.DoesNotContain("password", error.Message);
        store.Verify(x => x.ReleaseAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LostReservationAbortsOnlyItsConnectionAndReleasesOnce()
    {
        var store = SuccessfulStore();
        store.Setup(x => x.RenewAsync(10, "lease", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var lease = Lease(store.Object, () => aborted.TrySetResult()))
            await aborted.Task.WaitAsync(TimeSpan.FromSeconds(6));
        store.Verify(x => x.ReleaseAsync(10, "lease"), Times.Once);
    }

    [Fact]
    public async Task TemporaryRenewalFailureCanRecoverWithoutClosingExistingConnection()
    {
        var store = SuccessfulStore();
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        store.Setup(x => x.RenewAsync(10, "lease", It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (Interlocked.Increment(ref calls) == 1) return Task.FromException<bool>(new TimeoutException());
            renewed.TrySetResult();
            return Task.FromResult(true);
        });
        var aborted = false;
        await using (var lease = Lease(store.Object, () => aborted = true, leaseSeconds: 5))
        {
            await renewed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.False(aborted);
        }
        store.Verify(x => x.ReleaseAsync(10, "lease"), Times.Once);
    }

    [Fact]
    public async Task ProlongedRenewalFailureClosesBeforeExpiredSlotCanBeReused()
    {
        var store = SuccessfulStore();
        store.Setup(x => x.RenewAsync(10, "lease", It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var lease = Lease(store.Object, () => aborted.TrySetResult()))
            await aborted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        store.Verify(x => x.ReleaseAsync(10, "lease"), Times.Once);
    }

    [Theory]
    [InlineData("MaxConnectionsPerUser", "0")]
    [InlineData("MaxConnectionsPerUser", "51")]
    [InlineData("LeaseSeconds", "29")]
    [InlineData("RenewalSeconds", "0")]
    [InlineData("RenewalSeconds", "31")]
    public async Task UnsafeConfigurationIsRejected(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"SignalR:ConnectionLimits:{key}"] = value }).Build();
        var services = new ServiceCollection().AddLogging().AddRealtimeConnectionLimits(configuration);
        await using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RealtimeConnectionOptions>>().Value);
    }

    /// <summary>Tək nüsxə testlərində standart TTL və seçilmiş hesab limiti ilə atomik yaddaş store-u yaradır.</summary>
    private static MemoryRealtimeConnectionLeaseStore MemoryStore(int limit)
        => new(Options.Create(new RealtimeConnectionOptions { MaxConnectionsPerUser = limit }), TimeProvider.System);

    /// <summary>Filterin yalnız rezerv idarəetməsini yoxlamaq üçün hub kontekstini təhlükəsiz test kimliyi ilə qurur.</summary>
    private static HubLifetimeContext Context(string? userId, CancellationToken token = default)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.UserIdentifier).Returns(userId);
        context.SetupGet(x => x.ConnectionAborted).Returns(token);
        context.SetupGet(x => x.Items).Returns(new Dictionary<object, object?>());
        return new HubLifetimeContext(context.Object, Mock.Of<IServiceProvider>(), new UserEventsHub());
    }

    /// <summary>İstehsal filterini real xəta mətnləri və test store-u ilə yaradır.</summary>
    private static RealtimeConnectionLimitFilter Filter(IRealtimeConnectionLeaseStore store)
        => new(store, Options.Create(new RealtimeConnectionOptions()), TimeProvider.System, new ErrorMessageProvider(), NullLogger<RealtimeConnectionLimitFilter>.Instance);

    /// <summary>Rezervin normal alındığı və buraxıldığı ssenariləri yenilənmə xətalarından ayırır.</summary>
    private static Mock<IRealtimeConnectionLeaseStore> SuccessfulStore()
    {
        var store = new Mock<IRealtimeConnectionLeaseStore>();
        store.Setup(x => x.TryAcquireAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        store.Setup(x => x.RenewAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        store.Setup(x => x.ReleaseAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        return store;
    }

    /// <summary>Ürəkdöyünmə testini qısa real intervalla işlədir; istehsal ayarlarının minimumları DI testində ayrıca yoxlanılır.</summary>
    private static RealtimeConnectionLease Lease(IRealtimeConnectionLeaseStore store, Action abort, int leaseSeconds = 3)
        => new(store, 10, "lease", abort, CancellationToken.None, new RealtimeConnectionOptions { LeaseSeconds = leaseSeconds, RenewalSeconds = 1 },
            TimeProvider.System, NullLogger.Instance, TimeProvider.System.GetTimestamp());

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        /// <summary>Rezervin son tarixini gözləmədən yoxlamaq üçün test saatını qaytarır.</summary>
        public override DateTimeOffset GetUtcNow() => _now;
        /// <summary>Yalnız yaddaş store-un TTL sınağında UTC saatını irəli aparır.</summary>
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
