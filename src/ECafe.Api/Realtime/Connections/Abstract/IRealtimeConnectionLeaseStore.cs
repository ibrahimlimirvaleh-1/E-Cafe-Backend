namespace ECafe.Api.Realtime.Connections.Abstract;

public interface IRealtimeConnectionLeaseStore
{
    Task<bool> TryAcquireAsync(int userId, string leaseId, CancellationToken cancellationToken = default);
    Task<bool> RenewAsync(int userId, string leaseId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(int userId, string leaseId);
}
