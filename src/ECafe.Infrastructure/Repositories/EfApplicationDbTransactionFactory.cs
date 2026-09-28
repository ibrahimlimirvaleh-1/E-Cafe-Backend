using ECafe.Application.Repository;
using ECafe.Infrastructure.Context;
using ECafe.Application.Services.Realtime.Abstract;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories
{
    internal sealed class EfApplicationDbTransactionFactory : IApplicationDbTransactionFactory
    {
        private readonly ECafeDbContext _context;
        private readonly INotificationChangeDispatcher _notificationDispatcher;

        public EfApplicationDbTransactionFactory(
            ECafeDbContext context,
            INotificationChangeDispatcher notificationDispatcher)
        {
            _context = context;
            _notificationDispatcher = notificationDispatcher;
        }

        public async Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new EfApplicationDbTransaction(transaction, _notificationDispatcher);
        }

        public async Task<IApplicationDbTransaction> BeginTransactionAsync(
            System.Data.IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            return new EfApplicationDbTransaction(transaction, _notificationDispatcher);
        }
    }
}
