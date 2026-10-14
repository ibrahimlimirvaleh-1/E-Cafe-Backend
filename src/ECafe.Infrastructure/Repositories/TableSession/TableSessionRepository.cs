using ECafe.Application.Repositories.TableSession;
using ECafe.Application.DTOs.TableSession;
using ECafe.Shared.DTOs;
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

        public async Task<PaginatedList<WaiterTableSessionResponse>> GetOpenForWaiterAsync(
            int restaurantId, int waiterUserId, PaginationFilter pagination,
            CancellationToken cancellationToken = default)
        {
            var query = QueryOpenForWaiter(restaurantId, waiterUserId);
            var count = await query.CountAsync(cancellationToken);
            var skip = (long)(pagination.PageNumber - 1) * pagination.PageSize;
            var items = skip >= count
                ? new List<WaiterTableSessionResponse>()
                : await ProjectServiceView(query.OrderBy(session => session.OpenedAt).ThenBy(session => session.Id)
                    .Skip((int)skip).Take(pagination.PageSize)).ToListAsync(cancellationToken);

            return new PaginatedList<WaiterTableSessionResponse>(items, count, pagination.PageNumber, pagination.PageSize);
        }

        public Task<WaiterTableSessionResponse?> GetOpenByIdForWaiterAsync(
            int restaurantId, int waiterUserId, int tableSessionId,
            CancellationToken cancellationToken = default)
            => ProjectServiceView(QueryOpenForWaiter(restaurantId, waiterUserId)
                .Where(session => session.Id == tableSessionId)).SingleOrDefaultAsync(cancellationToken);

        /// <summary>
        /// Siyahı və detal üçün restoran, ofisiant və açıq sessiya sərhədlərini eyni tracking-siz sorğuda tətbiq edir.
        /// Tarixi keçmiş, amma hələ bağlanmamış sessiyanı gizlətmir; deaktiv masa da davam edən xidməti gizlətmir.
        /// </summary>
        private IQueryable<Domain.Entities.TableSession> QueryOpenForWaiter(int restaurantId, int waiterUserId)
            => Query(session => session.RestaurantId == restaurantId && session.WaiterUserId == waiterUserId &&
                session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && session.ClosedAt == null &&
                session.Restaurant.IsActive && session.Table.RestaurantId == restaurantId &&
                (session.Reservation == null || session.Reservation.RestaurantId == restaurantId));

        /// <summary>Yalnız xidmət üçün lazım olan sahələri seçir; tarixləri UTC offset ilə qaytarır, maliyyə və əlaqə məlumatlarını yükləmir.</summary>
        private static IQueryable<WaiterTableSessionResponse> ProjectServiceView(IQueryable<Domain.Entities.TableSession> query)
            => query.Select(session => new WaiterTableSessionResponse
            {
                Id = session.Id,
                RestaurantId = session.RestaurantId,
                TableId = session.TableId,
                TableNo = session.Table.TableNo,
                TableName = session.Table.Name,
                TableCapacity = session.Table.Capacity,
                StatusId = session.StatusId,
                ReservationId = session.ReservationId,
                CustomerName = session.CustomerUser == null ? null :
                    (session.CustomerUser.Name + " " + session.CustomerUser.Surname).Trim(),
                PeopleCount = session.Reservation == null ? null : session.Reservation.PeopleCount,
                OpenedAt = new DateTimeOffset(DateTime.SpecifyKind(session.OpenedAt, DateTimeKind.Utc)),
                MustVacateAt = session.Reservation == null || !session.Reservation.MustVacateAt.HasValue ? null :
                    new DateTimeOffset(DateTime.SpecifyKind(session.Reservation.MustVacateAt.Value, DateTimeKind.Utc)),
                TimeZone = session.Restaurant.TimeZone
            });

        public async Task<Dictionary<int, int>> GetOpenSessionCountsByWaitersAsync(
            int restaurantId,
            IReadOnlyCollection<int> waiterUserIds)
        {
            if (waiterUserIds.Count == 0)
                return [];

            var openStatusId = StatusIds.TableSession(TableSessionStatus.Open);

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
