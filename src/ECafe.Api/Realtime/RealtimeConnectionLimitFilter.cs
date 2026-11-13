using System.Globalization;
using ECafe.Api.Realtime.Connections;
using ECafe.Api.Realtime.Connections.Abstract;
using ECafe.Api.Realtime.Connections.Concrete;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ECafe.Api.Realtime;

public sealed class RealtimeConnectionLimitFilter(IRealtimeConnectionLeaseStore store,
    IOptions<RealtimeConnectionOptions> options, TimeProvider clock, IErrorMessageProvider messages,
    ILogger<RealtimeConnectionLimitFilter> logger) : IHubFilter
{
    private static readonly object LeaseKey = new();

    /// <summary>Hər məntiqi hub bağlantısını JWT-dən gələn hesab üzrə sayır; mövcud bağlantıları sıxışdırmadan artıq olanı rədd edir.</summary>
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        if (!int.TryParse(context.Context.UserIdentifier, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
            throw new HubException(messages.GetMessage(ErrorCode.SessionInvalid));

        var leaseId = Guid.NewGuid().ToString("N");
        var acquiredAt = clock.GetTimestamp();
        bool acquired;
        try { acquired = await store.TryAcquireAsync(userId, leaseId, context.Context.ConnectionAborted); }
        catch (OperationCanceledException) when (context.Context.ConnectionAborted.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogWarning("Realtime admission unavailable ({ErrorType}).", error.GetType().Name);
            throw new HubException(messages.GetMessage(ErrorCode.RealtimeTemporarilyUnavailable));
        }
        if (!acquired) throw new HubException(messages.GetMessage(ErrorCode.RealtimeConnectionLimitExceeded));

        var lease = new RealtimeConnectionLease(store, userId, leaseId, context.Context.Abort,
            context.Context.ConnectionAborted, options.Value, clock, logger, acquiredAt);
        context.Context.Items[LeaseKey] = lease;
        try
        {
            context.Context.ConnectionAborted.ThrowIfCancellationRequested();
            await next(context);
        }
        catch
        {
            context.Context.Items.Remove(LeaseKey);
            await lease.DisposeAsync();
            throw;
        }
    }

    /// <summary>Normal çıxış, şəbəkə kəsilməsi və hub xətasında rezervi buraxır; digər istifadəçi bağlantılarına toxunmur.</summary>
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        try
        {
            if (context.Context.Items.Remove(LeaseKey, out var lease) && lease is RealtimeConnectionLease connectionLease)
                await connectionLease.DisposeAsync();
        }
        finally { await next(context, exception); }
    }
}
