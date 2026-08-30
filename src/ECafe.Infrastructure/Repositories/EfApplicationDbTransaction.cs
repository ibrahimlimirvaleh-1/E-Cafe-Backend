using ECafe.Application.Repository;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECafe.Infrastructure.Repositories
{
    public sealed class EfApplicationDbTransaction : IApplicationDbTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationDbTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => _transaction.DisposeAsync();
    }
}
