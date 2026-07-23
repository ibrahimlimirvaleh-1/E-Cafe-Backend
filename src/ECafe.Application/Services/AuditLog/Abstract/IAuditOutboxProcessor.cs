namespace ECafe.Application.Services.AuditLog.Abstract
{
    public interface IAuditOutboxProcessor
    {
        Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}
