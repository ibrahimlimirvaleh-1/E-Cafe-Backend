using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.TableSession
{
    public interface ITableSessionRepository : IBaseRepository<Domain.Entities.TableSession>
    {
        Task<Dictionary<int, int>> GetOpenSessionCountsByWaitersAsync(
            int restaurantId,
            IReadOnlyCollection<int> waiterUserIds);
    }
}
