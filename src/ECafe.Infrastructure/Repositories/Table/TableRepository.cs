using ECafe.Application.Repositories.Table;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Table
{
    public class TableRepository : BaseRepository<Domain.Entities.Table>, ITableRepository
    {
        private static readonly int OpenTableSessionStatusId = StatusIds.TableSession(TableSessionStatus.Open);

        public TableRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<bool> HasTableWithoutOpenSessionAsync(int restaurantId)
        {
            return Query()
                .AnyAsync(t =>
                    t.RestaurantId == restaurantId &&
                    t.IsActive &&
                    !t.TableSessions.Any(ts =>
                        ts.RestaurantId == restaurantId &&
                        ts.StatusId == OpenTableSessionStatusId &&
                        ts.ClosedAt == null));
        }

        public Task<bool> HasOpenTableSessionAsync(int restaurantId, int tableId)
        {
            return Query()
                .Where(t =>
                    t.Id == tableId &&
                    t.RestaurantId == restaurantId &&
                    t.IsActive)
                .AnyAsync(t => t.TableSessions.Any(ts =>
                    ts.RestaurantId == restaurantId &&
                    ts.StatusId == OpenTableSessionStatusId &&
                    ts.ClosedAt == null));
        }

        public Task AcquireReservationLockAsync(
            int restaurantId,
            int tableId,
            CancellationToken cancellationToken = default)
        {
            // Transaction-scoped advisory locks serialize reservation attempts for one table.
            return Context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({restaurantId}, {tableId})",
                cancellationToken);
        }

        public async Task<bool> IsTableAvailableForReservationAsync(
            int restaurantId,
            int tableId,
            DateTimeOffset reservedAt)
        {
            var reservationInterval = await GetReservationIntervalUtcAsync(restaurantId, reservedAt);
            if (reservationInterval is null)
                return false;

            return await BuildAvailableTablesForReservationQuery(restaurantId, reservationInterval.Value)
                .AnyAsync(t => t.Id == tableId);
        }

        public async Task<bool> HasAvailableTableForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var reservationInterval = await GetReservationIntervalUtcAsync(restaurantId, reservedAt);
            if (reservationInterval is null)
                return false;

            return await BuildAvailableTablesForReservationQuery(restaurantId, reservationInterval.Value)
                .AnyAsync();
        }

        public async Task<List<Domain.Entities.Table>> GetAvailableTablesForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var reservationInterval = await GetReservationIntervalUtcAsync(restaurantId, reservedAt);
            if (reservationInterval is null)
                return [];

            return await BuildAvailableTablesForReservationQuery(restaurantId, reservationInterval.Value)
                .OrderBy(t => t.TableNo)
                .ToListAsync();
        }

        private IQueryable<Domain.Entities.Table> BuildAvailableTablesForReservationQuery(
            int restaurantId,
            ReservationIntervalUtc reservationInterval)
        {
            return Query()
                .Where(t =>
                    t.RestaurantId == restaurantId &&
                    t.IsActive &&
                    !t.TableSessions.Any(ts =>
                        ts.RestaurantId == restaurantId &&
                        ts.StatusId == OpenTableSessionStatusId &&
                        ts.ClosedAt == null) &&
                    !t.Reservations.Any(r =>
                        r.RestaurantId == restaurantId &&
                        r.TableId == t.Id &&
                        r.Status.BlocksTableAvailability &&
                        (r.StatusId != StatusIds.Reservation(ReservationStatus.PendingPayment) ||
                         (r.HoldExpiresAt != null && r.HoldExpiresAt > DateTime.UtcNow)) &&
                        r.ReservedAt >= reservationInterval.StartsAtUtc &&
                        r.ReservedAt < reservationInterval.EndsAtUtc));
        }

        private async Task<ReservationIntervalUtc?> GetReservationIntervalUtcAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var workingHours = await Context.RestaurantWorkingHours
                .AsNoTracking()
                .Where(wh => wh.RestaurantId == restaurantId && !wh.IsClosed)
                .ToListAsync();

            var timeZoneId = await Context.Restaurants
                .Where(restaurant => restaurant.Id == restaurantId && restaurant.IsActive)
                .Select(restaurant => restaurant.TimeZone)
                .SingleOrDefaultAsync();

            if (timeZoneId is null)
                return null;

            var restaurantLocalTime = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
                reservedAt.ToUniversalTime(),
                timeZoneId);

            return RestaurantWorkingHoursCalculator.TryGetActiveInterval(workingHours, restaurantLocalTime, out var interval)
                ? new ReservationIntervalUtc(interval.StartsAt.UtcDateTime, interval.EndsAt.UtcDateTime)
                : null;
        }

        private readonly record struct ReservationIntervalUtc(DateTime StartsAtUtc, DateTime EndsAtUtc);

    }
}
