using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repository;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Repositories.Reservation;

public interface IReservationRepository : IBaseRepository<Domain.Entities.Reservation>
{
    Task AcquireCustomerReservationLockAsync(
        int restaurantId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveReservationForCustomerOnUtcDayAsync(
        int restaurantId,
        int customerUserId,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForCustomerAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForCustomerWithHistoryAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForCustomerForUpdateAsync(
        int reservationId,
        int restaurantId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForCustomerSnapshotAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<Domain.Entities.Reservation>> GetForCustomerAsync(
        int customerUserId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ExpirePendingPaymentsAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<int> ExpireNoShowReservationsAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForRestaurantAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForRestaurantWithHistoryAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task<Domain.Entities.Reservation?> GetByIdForRestaurantSnapshotAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<Domain.Entities.Reservation>> GetForRestaurantAsync(
        int restaurantId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default);
}
