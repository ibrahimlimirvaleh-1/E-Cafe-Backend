using ECafe.Application.Repositories.ReservationRefund;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ReservationRefund;

public sealed class ReservationRefundRepository
    : BaseRepository<Domain.Entities.ReservationRefund>, IReservationRefundRepository
{
    public ReservationRefundRepository(ECafeDbContext context)
        : base(context)
    {
    }

    public Task<Domain.Entities.ReservationRefund?> GetByReservationForCustomerAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query(refund =>
                refund.ReservationId == reservationId &&
                refund.Reservation.CustomerUserId == customerUserId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Domain.Entities.ReservationRefund?> GetByReservationForRestaurantAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query(refund =>
                refund.ReservationId == reservationId &&
                refund.Reservation.RestaurantId == restaurantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Domain.Entities.ReservationRefund?> GetByIdForRestaurantSnapshotAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query(refund =>
                refund.Id == refundId &&
                refund.Reservation.RestaurantId == restaurantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Domain.Entities.ReservationRefund?> GetByIdForRestaurantForUpdateAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(QueryTracked(refund =>
                refund.Id == refundId &&
                refund.Reservation.RestaurantId == restaurantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Domain.Entities.ReservationRefund?> GetByIdForCustomerSnapshotAsync(
        int refundId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return Query(refund =>
                refund.Id == refundId &&
                refund.Reservation.CustomerUserId == customerUserId)
            .Include(refund => refund.Reservation)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Domain.Entities.ReservationRefund?> GetByIdForCustomerForUpdateAsync(
        int refundId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(QueryTracked(refund =>
                refund.Id == refundId &&
                refund.Reservation.CustomerUserId == customerUserId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsOwnedByCustomerAsync(
        int restaurantId,
        int refundId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return Query(refund =>
                refund.Id == refundId &&
                refund.Reservation.RestaurantId == restaurantId &&
                refund.Reservation.CustomerUserId == customerUserId)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> HasForReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        return Query(refund => refund.ReservationId == reservationId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> IsRequestAvailableAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        var isEligible = await Context.Reservations.AnyAsync(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.RefundEligible == true,
            cancellationToken);

        return isEligible && !await Context.ReservationRefunds.AnyAsync(
            refund => refund.ReservationId == reservationId,
            cancellationToken);
    }

    private static IQueryable<Domain.Entities.ReservationRefund> WithDetails(
        IQueryable<Domain.Entities.ReservationRefund> query)
    {
        return query
            .Include(refund => refund.Reservation)
            .Include(refund => refund.Status)
            .Include(refund => refund.PayoutDetails)
            .Include(refund => refund.StatusHistory)
                .ThenInclude(history => history.FromStatus)
            .Include(refund => refund.StatusHistory)
                .ThenInclude(history => history.ToStatus);
    }
}
