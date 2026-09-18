using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Reservation
{
    public interface IReservationRepository : IBaseRepository<Domain.Entities.Reservation>
    {
        Task<Domain.Entities.Reservation?> GetByIdForCustomerAsync(
            int reservationId,
            int customerUserId,
            CancellationToken cancellationToken = default);

        Task<int> ExpirePendingPaymentsAsync(
            DateTime nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default);

        Task<Domain.Entities.Reservation?> GetByIdForRestaurantAsync(
            int reservationId,
            int restaurantId,
            CancellationToken cancellationToken = default);
    }
}
