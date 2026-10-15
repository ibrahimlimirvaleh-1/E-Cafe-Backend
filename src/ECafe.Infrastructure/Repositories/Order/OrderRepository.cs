using ECafe.Application.Repositories.Order;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Order;

public sealed class OrderRepository(ECafeDbContext context) : BaseRepository<Domain.Entities.Order>(context), IOrderRepository
{
    /// <summary>Silinmiş sifarişin açarını da tapır ki, eyni sorğu sonradan yeni sifariş yaratmasın; scope hər zaman restoran və sessiya ilə məhdudlaşır.</summary>
    public Task<Domain.Entities.Order?> FindCreationRequestAsync(int restaurantId, int tableSessionId,
        Guid requestId, CancellationToken cancellationToken = default)
        => Context.Orders.IgnoreQueryFilters().AsNoTracking()
            .Where(order => order.RestaurantId == restaurantId && order.TableSessionId == tableSessionId &&
                order.CreationRequestId == requestId)
            .Include(order => order.OrderItems.Where(item => !item.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
}
