using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ECafe.Infrastructure.Context;
using ECafe.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Reservation;

public class ReservationRepository : BaseRepository<Domain.Entities.Reservation>, IReservationRepository
{
    public ReservationRepository(ECafeDbContext context) : base(context)
    {
    }

    public Task<Domain.Entities.Reservation?> GetForArrivalAdjustmentAsync(
        int reservationId, int customerUserId, bool tracked, CancellationToken cancellationToken = default)
    {
        var query = tracked ? QueryTracked() : Query();
        return query.Include(r => r.ArrivalAdjustment)
            .Include(r => r.Restaurant).ThenInclude(r => r.WorkingHours)
            .Include(r => r.Table)
            .Include(r => r.TableSessions)
            .Include(r => r.PaymentProofs)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.CustomerUserId == customerUserId, cancellationToken);
    }

    public Task AcquireCustomerReservationLockAsync(
        int restaurantId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        // Negative second key keeps this lock namespace separate from table locks.
        return Context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({restaurantId}, {-customerUserId})",
            cancellationToken);
    }

    public Task<bool> HasActiveReservationForCustomerOnUtcDayAsync(
        int restaurantId,
        int customerUserId,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
        var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

        return Query().AnyAsync(reservation =>
            reservation.RestaurantId == restaurantId &&
            reservation.CustomerUserId == customerUserId &&
            reservation.Status.BlocksTableAvailability &&
            reservation.ReservedAt >= dayStartUtc &&
            reservation.ReservedAt < dayEndUtc &&
            ((reservation.StatusId == awaitingPaymentInstructionStatusId &&
              reservation.RestaurantResponseExpiresAt != null &&
              reservation.RestaurantResponseExpiresAt > nowUtc) ||
             (reservation.StatusId == pendingPaymentStatusId &&
              (reservation.HoldExpiresAt == null || reservation.HoldExpiresAt > nowUtc)) ||
             (reservation.StatusId != awaitingPaymentInstructionStatusId &&
              reservation.StatusId != pendingPaymentStatusId)),
            cancellationToken);
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

    public Task<bool> IsOwnedByCustomerAsync(
        int restaurantId,
        int reservationId,
        int customerUserId,
        CancellationToken cancellationToken = default)
    {
        return Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.CustomerUserId == customerUserId)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> IsReservedTimePassedAsync(
        int restaurantId,
        int reservationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.ReservedAt <= nowUtc)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> IsCancellationDeadlinePassedAsync(
        int restaurantId,
        int reservationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.CancellationDeadline.HasValue &&
                reservation.CancellationDeadline.Value <= nowUtc)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> IsCheckInWindowOpenAsync(
        int restaurantId,
        int reservationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.ReservedAt <= nowUtc &&
                (reservation.MustVacateAt == null || reservation.MustVacateAt > nowUtc) &&
                (reservation.NoShowDeadlineAt > nowUtc || reservation.ArrivedAt != null))
            .AnyAsync(cancellationToken);
    }

    public Task<bool> IsMarkArrivalWindowOpenAsync(
        int restaurantId,
        int reservationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return Query(reservation =>
                reservation.Id == reservationId &&
                reservation.RestaurantId == restaurantId &&
                reservation.ReservedAt <= nowUtc &&
                reservation.NoShowDeadlineAt > nowUtc &&
                (reservation.MustVacateAt == null || reservation.MustVacateAt > nowUtc) &&
                reservation.ArrivedAt == null)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> IsRestaurantOpenAsync(
        int restaurantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await Context.Restaurants.AsNoTracking()
            .Include(item => item.WorkingHours)
            .SingleOrDefaultAsync(item => item.Id == restaurantId && item.IsActive, cancellationToken);
        if (restaurant is null)
            return false;

        var localTime = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
            new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)),
            restaurant.TimeZone);
        return RestaurantWorkingHoursCalculator.TryGetActiveInterval(
            restaurant.WorkingHours, localTime, out _);
    }

    public Task<PaginatedList<Domain.Entities.Reservation>> GetForCustomerAsync(
        int customerUserId,
        MyReservationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails(Query().Where(r => r.CustomerUserId == customerUserId));

        var restaurantName = request.RestaurantName?.Trim();
        if (!string.IsNullOrEmpty(restaurantName))
        {
            var escapedName = restaurantName.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            query = query.Where(r => EF.Functions.ILike(r.Restaurant.Name, $"%{escapedName}%", @"\"));
        }

        return CreatePageAsync(query, request, cancellationToken);
    }

    public Task<List<ReservationPendingExpiryCandidate>> GetPendingExpiryCandidatesAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return Task.FromResult(new List<ReservationPendingExpiryCandidate>());

        return GetPendingExpiryEligibleReservationsQuery(nowUtc)
            .AsNoTracking()
            .OrderBy(r => r.RestaurantResponseExpiresAt ?? r.HoldExpiresAt)
            .ThenBy(r => r.Id)
            .Take(batchSize)
            .Select(r => new ReservationPendingExpiryCandidate(r.Id, r.RestaurantId, r.TableId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryExpirePendingReservationAsync(
        ReservationPendingExpiryCandidate candidate,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // The caller holds the table lock; recheck status and deadline after waiting for it.
        var reservation = await GetPendingExpiryEligibleReservationsQuery(nowUtc)
            .FirstOrDefaultAsync(r =>
                r.Id == candidate.ReservationId &&
                r.RestaurantId == candidate.RestaurantId &&
                r.TableId == candidate.TableId,
                cancellationToken);

        if (reservation is null)
            return false;

        var previousStatusId = reservation.StatusId;
        var expiredStatusId = StatusIds.Reservation(ReservationStatus.Expired);
        reservation.StatusId = expiredStatusId;
        Context.ReservationStatusHistory.Add(new ReservationStatusHistory
        {
            ReservationId = reservation.Id,
            FromStatusId = previousStatusId,
            ToStatusId = expiredStatusId,
            ChangedAt = nowUtc,
            Reason = "Rezervasiyanın cavab və ya ödəniş müddəti bitdi."
        });

        await Context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Domain.Entities.Reservation> GetPendingExpiryEligibleReservationsQuery(DateTime nowUtc)
    {
        var awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
        var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

        return QueryTracked().Where(r =>
                (r.StatusId == awaitingPaymentInstructionStatusId &&
                 r.RestaurantResponseExpiresAt != null &&
                 r.RestaurantResponseExpiresAt <= nowUtc) ||
                (r.StatusId == pendingPaymentStatusId &&
                 (r.HoldExpiresAt == null || r.HoldExpiresAt <= nowUtc)));
    }

    public Task<List<ReservationNoShowCandidate>> GetNoShowCandidatesAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return Task.FromResult(new List<ReservationNoShowCandidate>());

        return GetNoShowEligibleReservationsQuery(nowUtc)
            .AsNoTracking()
            .OrderBy(reservation => reservation.NoShowDeadlineAt)
            .ThenBy(reservation => reservation.Id)
            .Take(batchSize)
            .Select(reservation => new ReservationNoShowCandidate(
                reservation.Id, reservation.RestaurantId, reservation.TableId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryExpireNoShowReservationAsync(
        ReservationNoShowCandidate candidate,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // The caller holds the table lock; recheck eligibility instead of trusting the batch snapshot.
        var reservation = await GetNoShowEligibleReservationsQuery(nowUtc)
            .FirstOrDefaultAsync(reservation =>
                reservation.Id == candidate.ReservationId &&
                reservation.RestaurantId == candidate.RestaurantId &&
                reservation.TableId == candidate.TableId,
                cancellationToken);

        if (reservation is null)
            return false;

        var confirmedStatusId = StatusIds.Reservation(ReservationStatus.Confirmed);
        var noShowStatusId = StatusIds.Reservation(ReservationStatus.NoShow);

        reservation.StatusId = noShowStatusId;
        reservation.NoShowAt = nowUtc;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            ReservationId = reservation.Id,
            FromStatusId = confirmedStatusId,
            ToStatusId = noShowStatusId,
            ChangedAt = nowUtc,
            Reason = "Müştəri gəliş üçün ayrılan vaxtda restorana gəlmədi."
        });

        await Context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Domain.Entities.Reservation> GetNoShowEligibleReservationsQuery(DateTime nowUtc)
    {
        var confirmedStatusId = StatusIds.Reservation(ReservationStatus.Confirmed);
        var openSessionStatusId = StatusIds.TableSession(TableSessionStatus.Open);

        return QueryTracked().Where(reservation =>
            reservation.StatusId == confirmedStatusId &&
            reservation.ArrivedAt == null &&
            reservation.NoShowDeadlineAt <= nowUtc &&
            !(reservation.ArrivalAdjustment != null &&
              reservation.ArrivalAdjustment.AcceptedAt == null &&
              reservation.ArrivalAdjustment.DecisionExpiresAt > nowUtc) &&
            !reservation.TableSessions.Any(session =>
                session.StatusId == openSessionStatusId && session.ClosedAt == null));
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
        RestaurantReservationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails(Query().Where(r => r.RestaurantId == restaurantId));
        if (request.TableId.HasValue)
            query = query.Where(r => r.TableId == request.TableId.Value);
        return CreatePageAsync(query, request, cancellationToken);
    }

    public async Task<PaginatedList<Domain.Entities.Reservation>> GetForServiceAsync(
        int restaurantId,
        RestaurantReservationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var confirmedStatusId = StatusIds.Reservation(ReservationStatus.Confirmed);
        var seatedStatusId = StatusIds.Reservation(ReservationStatus.Seated);
        IQueryable<Domain.Entities.Reservation> query = Query().Where(reservation =>
                reservation.RestaurantId == restaurantId &&
                (reservation.StatusId == confirmedStatusId || reservation.StatusId == seatedStatusId))
            .Include(reservation => reservation.Table)
            .Include(reservation => reservation.Restaurant)
            .Include(reservation => reservation.CustomerUser);

        if (request.TableId.HasValue)
            query = query.Where(reservation => reservation.TableId == request.TableId.Value);

        if (request.ReservedDate.HasValue)
        {
            var timeZone = await Context.Restaurants.AsNoTracking()
                .Where(restaurant => restaurant.Id == restaurantId)
                .Select(restaurant => restaurant.TimeZone)
                .SingleOrDefaultAsync(cancellationToken);
            var localDate = DateOnly.FromDateTime(request.ReservedDate.Value.Date);
            var (startUtc, endUtc) = RestaurantTimeZoneConverter.GetUtcDayRange(localDate, timeZone);
            query = query.Where(reservation =>
                reservation.ReservedAt >= startUtc && reservation.ReservedAt < endUtc);
        }

        return await CreatePageAsync(query, request, cancellationToken, filterReservedDate: false);
    }

    private static IQueryable<Domain.Entities.Reservation> WithDetails(
        IQueryable<Domain.Entities.Reservation> query)
    {
        return query
            .Include(r => r.Status)
            .Include(r => r.ArrivalAdjustment)
            .Include(r => r.Restaurant)
            .Include(r => r.Table)
            .Include(r => r.CustomerUser)
            .Include(r => r.PaymentInstructions
                .OrderByDescending(instruction => instruction.SentAt)
                .ThenByDescending(instruction => instruction.Id)
                .Take(1))
            .Include(r => r.PaymentProofs
                .OrderByDescending(proof => proof.SubmittedAt)
                .ThenByDescending(proof => proof.Id)
                .Take(1))
                .ThenInclude(proof => proof.Status)
            .Include(r => r.Refunds)
                .ThenInclude(refund => refund.Status);
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
        CancellationToken cancellationToken,
        bool filterReservedDate = true)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (request.StatusId.HasValue)
            query = query.Where(r => r.StatusId == request.StatusId.Value);

        if (filterReservedDate && request.ReservedDate.HasValue)
        {
            var dayStartUtc = request.ReservedDate.Value.UtcDateTime;
            var nextDayStartUtc = dayStartUtc.AddDays(1);
            query = query.Where(r => r.ReservedAt >= dayStartUtc && r.ReservedAt < nextDayStartUtc);
        }

        query = query.OrderByDescending(r => r.ReservedAt).ThenByDescending(r => r.Id);
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<Domain.Entities.Reservation>(items, count, pageNumber, pageSize);
    }
}
