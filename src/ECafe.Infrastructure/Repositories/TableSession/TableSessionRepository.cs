using ECafe.Application.Repositories.TableSession;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.TableSession
{
    public class TableSessionRepository : BaseRepository<Domain.Entities.TableSession>, ITableSessionRepository
    {
        public TableSessionRepository(ECafeDbContext context) : base(context)
        {
        }

        public async Task<Dictionary<int, int>> GetOpenSessionCountsByWaitersAsync(
            int restaurantId,
            IReadOnlyCollection<int> waiterUserIds)
        {
            if (waiterUserIds.Count == 0)
                return [];

            var openStatusId = ((int)StatusType.TableSession * 1000) + (int)TableSessionStatus.Open;

            return await Query()
                .Where(session =>
                    session.RestaurantId == restaurantId &&
                    session.WaiterUserId.HasValue &&
                    waiterUserIds.Contains(session.WaiterUserId.Value) &&
                    session.StatusId == openStatusId)
                .GroupBy(session => session.WaiterUserId!.Value)
                .Select(group => new
                {
                    WaiterUserId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(x => x.WaiterUserId, x => x.Count);
        }
    }
}
