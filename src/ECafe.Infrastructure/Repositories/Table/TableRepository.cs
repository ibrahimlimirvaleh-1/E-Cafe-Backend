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

            return await BuildReservationAvailabilityQuery(
                    restaurantId,
                    reservedAt.UtcDateTime,
                    policy.Value)
                .OrderBy(result => result.Table.TableNo)
                .Select(result => new ReservationTableAvailability(
                    result.Table,
                    result.NextReservationAt == null
                        ? null
                        : result.NextReservationAt.Value.AddMinutes(-policy.Value.TableTurnoverBufferMinutes)))
                .ToListAsync();
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

            var result = await BuildReservationAvailabilityQuery(
                    restaurantId,
                    reservedAt.UtcDateTime,
                    policy.Value,
                    excludedReservationId)
                .Where(candidate => candidate.Table.Id == tableId)
                .Select(candidate => new ReservationTableAvailability(
                    candidate.Table,
                    candidate.NextReservationAt == null
                        ? null
                        : candidate.NextReservationAt.Value.AddMinutes(-policy.Value.TableTurnoverBufferMinutes)))
                .SingleOrDefaultAsync();

            return result;
        }

        private IQueryable<ReservationAvailabilityCandidate> BuildReservationAvailabilityQuery(
            int restaurantId,
            DateTime reservedAtUtc,
            ReservationAvailabilityPolicy policy,
            int? excludedReservationId = null)
        {
            var blockingReservations = BuildBlockingReservationsQuery(
                restaurantId,
                DateTime.UtcNow,
                excludedReservationId);

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
                        r.ReservedAt <= reservedAtUtc))
                .Select(t => new ReservationAvailabilityCandidate(
                    t,
                    blockingReservations
                        .Where(r => r.TableId == t.Id && r.ReservedAt > reservedAtUtc)
                        .OrderBy(r => r.ReservedAt)
                        .Select(r => (DateTime?)r.ReservedAt)
                        .FirstOrDefault()))
                .Where(candidate =>
                    candidate.NextReservationAt == null ||
                    reservedAtUtc < candidate.NextReservationAt.Value.AddMinutes(-policy.ReservationPreBlockMinutes));
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

        private sealed record ReservationAvailabilityCandidate(
            Domain.Entities.Table Table,
            DateTime? NextReservationAt);

    }
}
