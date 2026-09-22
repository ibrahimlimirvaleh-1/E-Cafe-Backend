using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Reservation;

public class ReservationRepository : BaseRepository<Domain.Entities.Reservation>, IReservationRepository
{
    public ReservationRepository(ECafeDbContext context) : base(context)
    {
    }

    public Task<Domain.Entities.Reservation?> GetByIdForCustomerAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query())
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId &&
                               reservation.CustomerUserId == customerUserId,
                cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForCustomerWithHistoryAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithHistory(Query())
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId &&
                               reservation.CustomerUserId == customerUserId,
                cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForCustomerForUpdateAsync(
        int reservationId,
        int restaurantId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(QueryTracked())
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId &&
                               reservation.RestaurantId == restaurantId &&
                               reservation.CustomerUserId == customerUserId,
                cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForCustomerSnapshotAsync(
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query())
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId &&
                               reservation.CustomerUserId == customerUserId,
                cancellationToken);
    }

    public Task<PaginatedList<Domain.Entities.Reservation>> GetForCustomerAsync(
        int customerUserId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails(Query().Where(r => r.CustomerUserId == customerUserId));
        return CreatePageAsync(query, request, cancellationToken);
    }

    public async Task<int> ExpirePendingPaymentsAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return 0;

        var awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
        var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        var expiredStatusId = StatusIds.Reservation(ReservationStatus.Expired);

        var reservations = await QueryTracked()
            .Where(r =>
                (r.StatusId == awaitingPaymentInstructionStatusId &&
                 r.RestaurantResponseExpiresAt != null &&
                 r.RestaurantResponseExpiresAt <= nowUtc) ||
                (r.StatusId == pendingPaymentStatusId &&
                 (r.HoldExpiresAt == null || r.HoldExpiresAt <= nowUtc)))
            .OrderBy(r => r.RestaurantResponseExpiresAt ?? r.HoldExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
            return 0;

        foreach (var reservation in reservations)
        {
            var previousStatusId = reservation.StatusId;
            reservation.StatusId = expiredStatusId;
            Context.ReservationStatusHistory.Add(new ReservationStatusHistory
            {
                ReservationId = reservation.Id,
                FromStatusId = previousStatusId,
                ToStatusId = expiredStatusId,
                ChangedAt = nowUtc,
                Reason = "Rezervasiyanın cavab və ya ödəniş müddəti bitdi."
            });
        }

        return await Context.SaveChangesAsync(cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForRestaurantAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(QueryTracked())
            .FirstOrDefaultAsync(
                r => r.Id == reservationId && r.RestaurantId == restaurantId,
                cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForRestaurantWithHistoryAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return WithHistory(Query())
            .FirstOrDefaultAsync(
                r => r.Id == reservationId && r.RestaurantId == restaurantId,
                cancellationToken);
    }

    public Task<Domain.Entities.Reservation?> GetByIdForRestaurantSnapshotAsync(
        int reservationId,
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return WithDetails(Query())
            .FirstOrDefaultAsync(
                r => r.Id == reservationId && r.RestaurantId == restaurantId,
                cancellationToken);
    }

    public Task<PaginatedList<Domain.Entities.Reservation>> GetForRestaurantAsync(
        int restaurantId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails(Query().Where(r => r.RestaurantId == restaurantId));
        return CreatePageAsync(query, request, cancellationToken);
    }

    private static IQueryable<Domain.Entities.Reservation> WithDetails(
        IQueryable<Domain.Entities.Reservation> query)
    {
        return query
            .Include(r => r.Status)
            .Include(r => r.Restaurant)
            .Include(r => r.Table)
            .Include(r => r.CustomerUser)
            .Include(r => r.PaymentInstructions
                .OrderByDescending(instruction => instruction.SentAt)
                .Take(1))
            .Include(r => r.PaymentProofs
                .OrderByDescending(proof => proof.SubmittedAt)
                .ThenByDescending(proof => proof.Id)
                .Take(1))
                .ThenInclude(proof => proof.Status);
    }

    private static IQueryable<Domain.Entities.Reservation> WithHistory(
        IQueryable<Domain.Entities.Reservation> query)
    {
        return query
            .Include(r => r.CustomerUser)
            .Include(r => r.StatusHistory)
                .ThenInclude(history => history.FromStatus)
            .Include(r => r.StatusHistory)
                .ThenInclude(history => history.ToStatus);
    }

    private static async Task<PaginatedList<Domain.Entities.Reservation>> CreatePageAsync(
        IQueryable<Domain.Entities.Reservation> query,
        ReservationQueryRequest request,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (request.StatusId.HasValue)
            query = query.Where(r => r.StatusId == request.StatusId.Value);

        query = query.OrderByDescending(r => r.ReservedAt).ThenByDescending(r => r.Id);
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<Domain.Entities.Reservation>(items, count, pageNumber, pageSize);
    }
}
