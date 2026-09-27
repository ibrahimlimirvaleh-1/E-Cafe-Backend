using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.ReservationPaymentProof;

public sealed class ReservationPaymentProofRepository
    : BaseRepository<Domain.Entities.ReservationPaymentProof>,
      IReservationPaymentProofRepository
{
    public ReservationPaymentProofRepository(ECafeDbContext context)
        : base(context)
    {
    }
}
