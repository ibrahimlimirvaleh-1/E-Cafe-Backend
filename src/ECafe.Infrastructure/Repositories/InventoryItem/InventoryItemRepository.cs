using ECafe.Application.Repositories.InventoryItem;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.InventoryItem
{
    public class InventoryItemRepository : BaseRepository<Domain.Entities.InventoryItem>, IInventoryItemRepository
    {
        public InventoryItemRepository(ECafeDbContext context) : base(context)
        {
        }

        /// <summary>Yoxlanmış izlənən anbar məhsulunun deaktiv dillər daxil bütün tərcümələrini dəyişiklik üçün yükləyir.</summary>
        public Task LoadTranslationsAsync(Domain.Entities.InventoryItem owner, CancellationToken cancellationToken = default)
            => Context.Entry(owner).Collection(entity => entity.Translations).Query()
                .IgnoreQueryFilters().LoadAsync(cancellationToken);

        public Task<Domain.Entities.InventoryItem?> GetInventoryByRestaurantIdAsync(int id, int restaurantId)
            => QueryTracked().FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == restaurantId);

   
        
    }
}
