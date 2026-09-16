namespace ECafe.Application.Repository
{
    public interface IApplicationDbTransactionFactory
    {
        Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        Task<IApplicationDbTransaction> BeginTransactionAsync(
            System.Data.IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default);
    }
}
