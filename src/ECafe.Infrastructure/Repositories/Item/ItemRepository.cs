using ECafe.Application.Repositories.Item;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Item
{
    public class ItemRepository : BaseRepository<Domain.Entities.Item>, IItemRepository
    {
        public ItemRepository(ECafeDbContext context) : base(context)
        {
        }

        /// <summary>Yoxlanmış izlənən məhsulun deaktiv dillər daxil bütün tərcümələrini dəyişiklik üçün yükləyir.</summary>
        public Task LoadTranslationsAsync(Domain.Entities.Item owner, CancellationToken cancellationToken = default)
            => Context.Entry(owner).Collection(entity => entity.Translations).Query()
                .IgnoreQueryFilters().LoadAsync(cancellationToken);
    }
}
