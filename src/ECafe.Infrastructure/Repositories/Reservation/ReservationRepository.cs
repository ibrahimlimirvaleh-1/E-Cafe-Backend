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

        public Task<List<Domain.Entities.Reservation>> GetExpiredPendingPaymentsAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken)
        {
            var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

            return QueryTracked()
                    .Where(r =>
                        r.StatusId == pendingPaymentStatusId &&
                        r.HoldExpiresAt != null &&
                        r.HoldExpiresAt <= nowUtc)
                    .OrderBy(r => r.HoldExpiresAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
        }
    }
}
