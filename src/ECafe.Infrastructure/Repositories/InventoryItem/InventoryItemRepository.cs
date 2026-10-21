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

        /// <summary>Qalıq oxunmazdan əvvəl anbar sətirlərini sabit ID sırası ilə transaction sonunadək kilidləyir; müxtəlif masalarda artıq satışın qarşısını alır.</summary>
        public Task LockForStockChangeAsync(int restaurantId, IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
        {
            if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("Stock mutation requires a transaction.");
            if (ids.Count == 0) return Task.CompletedTask;
            var keys = ids.Distinct().Order().ToArray();
            return Context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM inventory.inventory_items WHERE restaurant_id = {restaurantId} AND id = ANY({keys}) ORDER BY id FOR UPDATE",
                cancellationToken);
        }

   
        
    }
}
