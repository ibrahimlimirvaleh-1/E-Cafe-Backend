using ECafe.Application.Repositories.Reservation;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Reservation
{
    public class ReservationRepository : BaseRepository<Domain.Entities.Reservation>, IReservationRepository
    {
        public ReservationRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<Domain.Entities.Reservation?> GetByIdForCustomerAsync(
            int reservationId,
            int customerUserId,
            CancellationToken cancellationToken = default)
        {
            return Query()
                .Include(reservation => reservation.Status)
                .FirstOrDefaultAsync(
                    reservation => reservation.Id == reservationId &&
                                   reservation.CustomerUserId == customerUserId,
                    cancellationToken);
        }

        public async Task<int> ExpirePendingPaymentsAsync(
            DateTime nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0)
                return 0;

            var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
            var expiredStatusId = StatusIds.Reservation(ReservationStatus.Expired);

            // Select a bounded batch first. The status/expiry predicates are
            // repeated in the update so a concurrent payment approval wins.
            var reservationIds = await Query()
                .Where(r =>
                    r.StatusId == pendingPaymentStatusId &&
                    r.HoldExpiresAt != null &&
                    r.HoldExpiresAt <= nowUtc)
                .OrderBy(r => r.HoldExpiresAt)
                .Take(batchSize)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (reservationIds.Count == 0)
                return 0;

            return await Context.Set<Domain.Entities.Reservation>()
                .Where(r =>
                    reservationIds.Contains(r.Id) &&
                    r.StatusId == pendingPaymentStatusId &&
                    r.HoldExpiresAt != null &&
                    r.HoldExpiresAt <= nowUtc)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        r => r.StatusId,
                        expiredStatusId),
                    cancellationToken);
        }
    }
}
