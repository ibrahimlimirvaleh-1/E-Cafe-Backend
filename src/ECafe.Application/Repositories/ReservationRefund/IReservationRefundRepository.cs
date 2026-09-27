using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.ReservationRefund;

public interface IReservationRefundRepository : IBaseRepository<Domain.Entities.ReservationRefund>
{
    Task<Domain.Entities.ReservationRefund?> GetByReservationForCustomerAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.ReservationRefund?> GetByReservationForRestaurantAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.ReservationRefund?> GetByIdForRestaurantSnapshotAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.ReservationRefund?> GetByIdForRestaurantForUpdateAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default);

    Task<bool> HasForReservationAsync(int reservationId, CancellationToken cancellationToken = default);

    Task<bool> IsRequestAvailableAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);
}
