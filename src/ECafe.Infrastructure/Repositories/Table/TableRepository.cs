using ECafe.Application.Repositories.Table;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Entities;
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
            DateTimeOffset reservedAt,
            int? excludedReservationId = null)
        {
            return await GetReservationTableAvailabilityAsync(
                    restaurantId,
                    tableId,
                    reservedAt,
                    excludedReservationId) is not null;
        }

        public async Task<bool> HasAvailableTableForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            return (await GetReservationTableAvailabilityAsync(restaurantId, reservedAt)).Count > 0;
        }

        public async Task<List<Domain.Entities.Table>> GetAvailableTablesForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            return (await GetReservationTableAvailabilityAsync(restaurantId, reservedAt))
                .Select(availability => availability.Table)
                .ToList();
        }

        public async Task<List<ReservationTableAvailability>> GetReservationTableAvailabilityAsync(
            int restaurantId,
            DateTimeOffset reservedAt)
        {
            var policy = await GetReservationPolicyAsync(restaurantId, reservedAt);
            if (policy is null)
                return [];

            var reservedAtUtc = reservedAt.UtcDateTime;
            var blockingReservations = BuildBlockingReservationsQuery(
                restaurantId,
                DateTime.UtcNow,
                excludedReservationId: null,
                policy.Value);

            var candidates = await BuildAvailableReservationTablesQuery(
                    restaurantId,
                    reservedAtUtc,
                    policy.Value,
                    blockingReservations)
                .OrderBy(table => table.TableNo)
                .Select(table => new
                {
                    Table = table,
                    NextReservationAt = blockingReservations
                        .Where(reservation =>
                            reservation.TableId == table.Id &&
                            reservation.ReservedAt > reservedAtUtc)
                        .OrderBy(reservation => reservation.ReservedAt)
                        .Select(reservation => (DateTime?)reservation.ReservedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return candidates
                .Select(candidate => new ReservationTableAvailability(
                    candidate.Table,
                    candidate.NextReservationAt?.AddMinutes(-policy.Value.TableTurnoverBufferMinutes)))
                .ToList();
        }

        public async Task<ReservationTableAvailability?> GetReservationTableAvailabilityAsync(
            int restaurantId,
            int tableId,
            DateTimeOffset reservedAt,
            int? excludedReservationId = null)
        {
            var policy = await GetReservationPolicyAsync(restaurantId, reservedAt);
            if (policy is null)
                return null;

            var reservedAtUtc = reservedAt.UtcDateTime;
            var blockingReservations = BuildBlockingReservationsQuery(
                restaurantId,
                DateTime.UtcNow,
                excludedReservationId,
                policy.Value);

            var candidate = await BuildAvailableReservationTablesQuery(
                    restaurantId,
                    reservedAtUtc,
                    policy.Value,
                    blockingReservations)
                .Where(table => table.Id == tableId)
                .Select(table => new
                {
                    Table = table,
                    NextReservationAt = blockingReservations
                        .Where(reservation =>
                            reservation.TableId == table.Id &&
                            reservation.ReservedAt > reservedAtUtc)
                        .OrderBy(reservation => reservation.ReservedAt)
                        .Select(reservation => (DateTime?)reservation.ReservedAt)
                        .FirstOrDefault()
                })
                .SingleOrDefaultAsync();

            return candidate is null
                ? null
                : new ReservationTableAvailability(
                    candidate.Table,
                    candidate.NextReservationAt?.AddMinutes(-policy.Value.TableTurnoverBufferMinutes));
        }

        public async Task<DateTime?> GetNextReservationAtAsync(int restaurantId, int tableId,
            int excludedReservationId, DateTime originalReservedAtUtc, CancellationToken cancellationToken = default)
        {
            var policy = await GetReservationPolicyAsync(restaurantId,
                new DateTimeOffset(DateTime.SpecifyKind(originalReservedAtUtc, DateTimeKind.Utc)));
            if (policy is null)
                return null;

            return await BuildBlockingReservationsQuery(restaurantId, DateTime.UtcNow, excludedReservationId, policy.Value)
                .Where(r => r.TableId == tableId && r.ReservedAt > originalReservedAtUtc)
                .OrderBy(r => r.ReservedAt)
                .Select(r => (DateTime?)r.ReservedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private IQueryable<Domain.Entities.Table> BuildAvailableReservationTablesQuery(
            int restaurantId,
            DateTime reservedAtUtc,
            ReservationAvailabilityPolicy policy,
            IQueryable<Domain.Entities.Reservation> blockingReservations)
        {
            var reservationPreBlockEndsAt = reservedAtUtc.AddMinutes(policy.ReservationPreBlockMinutes);

            return Query()
                .Where(t =>
                    t.RestaurantId == restaurantId &&
                    t.IsActive &&
                    !t.TableSessions.Any(ts =>
                        ts.RestaurantId == restaurantId &&
                        ts.StatusId == OpenTableSessionStatusId &&
                        ts.ClosedAt == null) &&
                    !blockingReservations.Any(r =>
                        r.TableId == t.Id &&
                        r.ReservedAt <= reservationPreBlockEndsAt));
        }

        private IQueryable<Domain.Entities.Reservation> BuildBlockingReservationsQuery(
            int restaurantId,
            DateTime nowUtc,
            int? excludedReservationId,
            ReservationAvailabilityPolicy policy)
        {
            var awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
            var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

            return Context.Reservations.Where(r =>
                r.RestaurantId == restaurantId &&
                r.ReservedAt >= policy.IntervalStartsAtUtc &&
                r.ReservedAt < policy.IntervalEndsAtUtc &&
                (!excludedReservationId.HasValue || r.Id != excludedReservationId.Value) &&
                r.Status.BlocksTableAvailability &&
                ((r.StatusId == awaitingPaymentInstructionStatusId &&
                  r.RestaurantResponseExpiresAt != null &&
                  r.RestaurantResponseExpiresAt > nowUtc) ||
                 (r.StatusId == pendingPaymentStatusId &&
                  (r.HoldExpiresAt == null || r.HoldExpiresAt > nowUtc)) ||
                 (r.StatusId != awaitingPaymentInstructionStatusId &&
                  r.StatusId != pendingPaymentStatusId)));
        }

        private async Task<ReservationAvailabilityPolicy?> GetReservationPolicyAsync(
            int restaurantId,
            DateTimeOffset reservedAt)
        {
            var restaurant = await Context.Restaurants
                .AsNoTracking()
                .Include(item => item.WorkingHours)
                .SingleOrDefaultAsync(item => item.Id == restaurantId && item.IsActive);

            if (restaurant is null)
                return null;

            var pending = await Context.RestaurantScheduleChanges.AsNoTracking()
                .Where(c => c.RestaurantId == restaurantId && c.State == ScheduleChangeState.Pending)
                .Select(c => c.ProposedHoursJson).SingleOrDefaultAsync();
            if (pending != null && ScheduleTerms.IsAffected(restaurant.WorkingHours,
                    ScheduleTerms.Deserialize(pending), restaurant.TimeZone, reservedAt.UtcDateTime, null, out _))
                return null;
            var localTime = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
                reservedAt,
                restaurant.TimeZone);
            if (!RestaurantWorkingHoursCalculator.TryGetActiveInterval(
                    restaurant.WorkingHours,
                    localTime,
                    out var interval))
                return null;

            return new ReservationAvailabilityPolicy(
                restaurant.ReservationPreBlockMinutes,
                restaurant.TableTurnoverBufferMinutes,
                interval.StartsAt.UtcDateTime,
                interval.EndsAt.UtcDateTime);
        }

        private readonly record struct ReservationAvailabilityPolicy(
            int ReservationPreBlockMinutes,
            int TableTurnoverBufferMinutes,
            DateTime IntervalStartsAtUtc,
            DateTime IntervalEndsAtUtc);

    }
}
