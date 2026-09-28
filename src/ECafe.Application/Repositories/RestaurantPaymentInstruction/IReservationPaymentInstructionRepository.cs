using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.ReservationPaymentInstruction
{
    public interface IReservationPaymentInstructionRepository : IBaseRepository<Domain.Entities.ReservationPaymentInstruction>
    {
        Task<List<Domain.Entities.ReservationPaymentInstruction>>
            GetLegacyUnencryptedAsync(
                int batchSize,
                CancellationToken cancellationToken = default);
    }
}
