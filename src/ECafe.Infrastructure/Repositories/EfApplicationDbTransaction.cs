using ECafe.Application.Repository;
using ECafe.Application.Services.Realtime.Abstract;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECafe.Infrastructure.Repositories
{
    public sealed class EfApplicationDbTransaction : IApplicationDbTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly INotificationChangeDispatcher _notificationDispatcher;

        public EfApplicationDbTransaction(
            IDbContextTransaction transaction,
            INotificationChangeDispatcher notificationDispatcher)
        {
            _transaction = transaction;
            _notificationDispatcher = notificationDispatcher;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _notificationDispatcher.FlushAsync(CancellationToken.None);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            _notificationDispatcher.Discard();
            await _transaction.RollbackAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _notificationDispatcher.Discard();
            return _transaction.DisposeAsync();
        }
    }
}
