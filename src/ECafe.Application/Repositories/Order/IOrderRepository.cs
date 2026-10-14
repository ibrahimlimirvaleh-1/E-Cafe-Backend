using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Order;

public interface IOrderRepository : IBaseRepository<Domain.Entities.Order>
{
    Task<Domain.Entities.Order?> FindCreationRequestAsync(int restaurantId, int tableSessionId,
        Guid requestId, CancellationToken cancellationToken = default);
}
