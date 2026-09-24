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
            var policy = await GetReservationPolicyAsync(restaurantId);
            if (policy is null)
                return [];

            var reservedAtUtc = reservedAt.UtcDateTime;
            var blockingReservations = BuildBlockingReservationsQuery(
                restaurantId,
                DateTime.UtcNow,
                excludedReservationId: null);

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
            var policy = await GetReservationPolicyAsync(restaurantId);
            if (policy is null)
                return null;

            var reservedAtUtc = reservedAt.UtcDateTime;
            var blockingReservations = BuildBlockingReservationsQuery(
                restaurantId,
                DateTime.UtcNow,
                excludedReservationId);

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
            int? excludedReservationId)
        {
            var awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
            var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

            return Context.Reservations.Where(r =>
                r.RestaurantId == restaurantId &&
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

        private Task<ReservationAvailabilityPolicy?> GetReservationPolicyAsync(int restaurantId)
        {
            return Context.Restaurants
                .AsNoTracking()
                .Where(restaurant => restaurant.Id == restaurantId && restaurant.IsActive)
                .Select(restaurant => (ReservationAvailabilityPolicy?)new ReservationAvailabilityPolicy(
                    restaurant.ReservationPreBlockMinutes,
                    restaurant.TableTurnoverBufferMinutes))
                .SingleOrDefaultAsync();
        }

        private readonly record struct ReservationAvailabilityPolicy(
            int ReservationPreBlockMinutes,
            int TableTurnoverBufferMinutes);

    }
}
