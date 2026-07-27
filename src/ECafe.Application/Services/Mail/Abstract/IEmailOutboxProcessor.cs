namespace ECafe.Application.Services
{
    public interface IEmailOutboxProcessor
    {
        Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}
