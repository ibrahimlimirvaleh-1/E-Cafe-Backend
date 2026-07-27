using ECafe.Application.Repositories.InventoryItem;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.InventoryItem
{
    public class InventoryItemRepository : BaseRepository<Domain.Entities.InventoryItem>, IInventoryItemRepository
    {
        public InventoryItemRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
