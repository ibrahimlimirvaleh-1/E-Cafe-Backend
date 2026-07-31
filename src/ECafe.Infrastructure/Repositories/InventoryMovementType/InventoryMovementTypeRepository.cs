using ECafe.Application.Repositories.InventoryMovementType;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.InventoryMovementType
{
    public class InventoryMovementTypeRepository : BaseRepository<Domain.Entities.InventoryMovementType>, IInventoryMovementTypeRepository
    {
        public InventoryMovementTypeRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<Domain.Entities.InventoryMovementType?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            return Query()
                .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        }
    }
}
