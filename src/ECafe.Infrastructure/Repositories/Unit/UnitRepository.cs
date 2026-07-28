using ECafe.Application.Repositories.Unit;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.Unit
{
    public class UnitRepository : BaseRepository<Domain.Entities.Unit>, IUnitRepository
    {
        public UnitRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
