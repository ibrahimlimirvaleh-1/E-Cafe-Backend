using ECafe.Application.Repositories.Item;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.Item
{
    public class ItemRepository : BaseRepository<Domain.Entities.Item>, IItemRepository
    {
        public ItemRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
