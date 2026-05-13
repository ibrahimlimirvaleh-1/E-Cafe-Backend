using ECafe.Application.Repositories.Table;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.Table
{
    public class TableRepository : BaseRepository<Domain.Entities.Table>, ITableRepository
    {
        public TableRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
