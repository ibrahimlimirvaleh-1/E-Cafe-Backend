using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ReservationPaymentProof;

public sealed class ReservationPaymentProofRepository
    : BaseRepository<Domain.Entities.ReservationPaymentProof>,
      IReservationPaymentProofRepository
{
    public ReservationPaymentProofRepository(ECafeDbContext context)
        : base(context)
    {
    }

    public Task<Domain.Entities.ReservationPaymentProof?> GetLatestConfirmedByReservationAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        var confirmedStatusId = StatusIds.Reservation(ReservationStatus.Confirmed);

        return Query(proof =>
                proof.ReservationId == reservationId &&
                proof.StatusId == confirmedStatusId)
            .OrderByDescending(proof => proof.ReviewedAt ?? proof.SubmittedAt)
            .ThenByDescending(proof => proof.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
