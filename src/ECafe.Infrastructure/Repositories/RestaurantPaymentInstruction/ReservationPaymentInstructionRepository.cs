using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ReservationPaymentInstruction;

public class ReservationPaymentInstructionRepository
    : BaseRepository<Domain.Entities.ReservationPaymentInstruction>,
      IReservationPaymentInstructionRepository
{
    public ReservationPaymentInstructionRepository(ECafeDbContext context)
        : base(context)
    {
    }

    public Task<List<Domain.Entities.ReservationPaymentInstruction>>
        GetLegacyUnencryptedAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
    {
        return Context.ReservationPaymentInstructions
            .IgnoreQueryFilters()
            .Where(instruction =>
                instruction.EncryptedDetails == null &&
                instruction.LegacyDisplayText != null)
            .OrderBy(instruction => instruction.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
