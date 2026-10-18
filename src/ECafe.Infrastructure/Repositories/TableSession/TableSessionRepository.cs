using ECafe.Application.Repositories.TableSession;
using ECafe.Application.DTOs.TableSession;
using ECafe.Shared.DTOs;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using ECafe.Application.Services.TableSessions.Concrete;

namespace ECafe.Infrastructure.Repositories.TableSession
{
    public class TableSessionRepository : BaseRepository<Domain.Entities.TableSession>, ITableSessionRepository
    {
        private readonly TimeProvider _clock;
        /// <summary>Hesab üçün sifariş, depozit və sessiya ödənişlərini kartezyen hasil olmadan sabit saylı sorğularla yükləyir.</summary>
        public Task<Domain.Entities.TableSession?> GetForBillingAsync(int id, bool tracked, CancellationToken cancellationToken)
        {
            var query = tracked ? QueryTracked() : Query().AsNoTrackingWithIdentityResolution();
            return query.Where(session => session.Id == id)
                .Include(session => session.Orders).ThenInclude(order => order.OrderItems)
                .Include(session => session.Payments).Include(session => session.Restaurant)
                .Include(session => session.Reservation).ThenInclude(reservation => reservation!.Payments)
                .Include(session => session.Reservation).ThenInclude(reservation => reservation!.PaymentProofs)
                .Include(session => session.Reservation).ThenInclude(reservation => reservation!.Refunds)
                .Include(session => session.Reservation).ThenInclude(reservation => reservation!.TableSessions)
                .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        }
        public TableSessionRepository(ECafeDbContext context, TimeProvider? clock = null) : base(context)
        {
            _clock = clock ?? TimeProvider.System;
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
                    .Skip((int)skip).Take(pagination.PageSize), _clock.GetUtcNow().UtcDateTime).ToListAsync(cancellationToken);

            return new PaginatedList<WaiterTableSessionResponse>(items, count, pagination.PageNumber, pagination.PageSize);
        }

        public Task<WaiterTableSessionResponse?> GetOpenByIdForWaiterAsync(
            int restaurantId, int waiterUserId, int tableSessionId,
            CancellationToken cancellationToken = default)
            => ProjectServiceView(QueryOpenForWaiter(restaurantId, waiterUserId)
                .Where(session => session.Id == tableSessionId), _clock.GetUtcNow().UtcDateTime).SingleOrDefaultAsync(cancellationToken);

        /// <summary>
        /// Siyahı və detal üçün restoran, ofisiant və açıq sessiya sərhədlərini eyni tracking-siz sorğuda tətbiq edir.
        /// Tarixi keçmiş, amma hələ bağlanmamış sessiyanı gizlətmir; deaktiv masa da davam edən xidməti gizlətmir.
        /// </summary>
        private IQueryable<Domain.Entities.TableSession> QueryOpenForWaiter(int restaurantId, int waiterUserId)
            => Query(session => session.RestaurantId == restaurantId && session.WaiterUserId == waiterUserId &&
                session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && session.ClosedAt == null &&
                session.Restaurant.IsActive && session.Table.RestaurantId == restaurantId &&
                (session.Reservation == null || session.Reservation.RestaurantId == restaurantId));

        /// <summary>İş saatları ilə bağlanma yoxlamasını hesablayır və yalnız xidmət sahələrini qaytarır; API görünüşünə maliyyə və əlaqə məlumatı daxil etmir.</summary>
        private static IQueryable<WaiterTableSessionResponse> ProjectServiceView(IQueryable<Domain.Entities.TableSession> query, DateTime nowUtc)
            => query.Include(session => session.Table).Include(session => session.CustomerUser)
                .Include(session => session.Reservation).Include(session => session.Restaurant).ThenInclude(restaurant => restaurant.WorkingHours)
                .Select(session => TableSessionViewMapper.Map(session, nowUtc));

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
