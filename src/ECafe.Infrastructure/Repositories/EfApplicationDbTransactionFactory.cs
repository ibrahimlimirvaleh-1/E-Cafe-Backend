using ECafe.Application.Repository;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories
{
    internal sealed class EfApplicationDbTransactionFactory : IApplicationDbTransactionFactory
    {
        private readonly ECafeDbContext _context;

        public EfApplicationDbTransactionFactory(ECafeDbContext context)
        {
            _context = context;
        }

        public async Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new EfApplicationDbTransaction(transaction);
        }
    }
}
