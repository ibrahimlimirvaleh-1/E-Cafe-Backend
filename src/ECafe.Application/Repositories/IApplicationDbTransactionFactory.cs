namespace ECafe.Application.Repository
{
    public interface IApplicationDbTransactionFactory
    {
        Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
