using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Reservation
{
    public interface IReservationRepository : IBaseRepository<Domain.Entities.Reservation>
    {
        Task<Domain.Entities.Reservation?> GetByIdForCustomerAsync(
            int reservationId,
            int customerUserId,
            CancellationToken cancellationToken = default);
    }
}
