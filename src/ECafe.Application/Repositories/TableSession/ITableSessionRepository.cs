using ECafe.Application.Repository;
using ECafe.Application.DTOs.TableSession;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Repositories.TableSession
{
    public interface ITableSessionRepository : IBaseRepository<Domain.Entities.TableSession>
    {
        Task<PaginatedList<WaiterTableSessionResponse>> GetOpenForWaiterAsync(
            int restaurantId, int waiterUserId, PaginationFilter pagination,
            CancellationToken cancellationToken = default);

        Task<WaiterTableSessionResponse?> GetOpenByIdForWaiterAsync(
            int restaurantId, int waiterUserId, int tableSessionId,
            CancellationToken cancellationToken = default);

        Task<Dictionary<int, int>> GetOpenSessionCountsByWaitersAsync(
            int restaurantId,
            IReadOnlyCollection<int> waiterUserIds);
    }
}
