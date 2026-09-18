using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.ReservationPaymentInstruction;

public class ReservationPaymentInstructionRepository
    : BaseRepository<Domain.Entities.ReservationPaymentInstruction>,
      IReservationPaymentInstructionRepository
{
    public ReservationPaymentInstructionRepository(ECafeDbContext context)
        : base(context)
    {
    }
}
