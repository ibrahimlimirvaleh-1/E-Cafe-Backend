using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Session = ECafe.Domain.Entities.TableSession;

namespace ECafe.Application.Services.TableSessions.Concrete;

public sealed class TableSessionCloseReviewManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    ITableSessionRepository sessions, ITableRepository tables, IUserRestaurantRepository assignments,
    IApplicationDbTransactionFactory transactions, INotificationService notifications, IAuditLogService audit,
    IOptions<TableSessionReviewOptions> options, TimeProvider clock)
    : BaseManager(accessor, mapper, configuration), ITableSessionCloseReviewService
{
    public async Task<PaginatedList<WaiterTableSessionResponse>> GetReviewsAsync(int restaurantId, PaginationFilter pagination,
        CancellationToken cancellationToken = default)
    {
        await RequireManagerAsync(restaurantId);
        pagination = PaginationFilterNormalizer.Normalize(pagination);
        var now = clock.GetUtcNow().UtcDateTime;
        // Yalnız restorandakı açıq masalar yüklənir; köhnə nullable tarixlər də iş saatları ilə düzgün hesablanır.
        var candidates = await Details(OpenSessions().Where(session => session.RestaurantId == restaurantId &&
                (session.CloseReviewDueAt == null || session.CloseReviewDueAt <= now)))
            .ToListAsync(cancellationToken);
        var due = candidates.Where(session => TableSessionCloseReviewPolicy.RequiresReview(session, now))
            .OrderBy(TableSessionCloseReviewPolicy.GetDueAt).ThenBy(session => session.Id).ToList();
        var skip = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var page = skip >= due.Count ? [] : due.Skip((int)skip).Take(pagination.PageSize)
            .Select(session => TableSessionViewMapper.Map(session, now)).ToList();
        return new(page, due.Count, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<WaiterTableSessionResponse> ContinueServiceAsync(int restaurantId, int sessionId,
        ContinueTableServiceRequest request, CancellationToken cancellationToken = default)
    {
        await RequireManagerAsync(restaurantId);
        if (request == null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > options.Value.MaxReasonLength)
            throw new BadRequestException(ErrorCode.InvalidTableSessionReview);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var session = await LoadLockedAsync(restaurantId, sessionId, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
        await RequireManagerAsync(restaurantId);
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ReviewAgainAt.UtcDateTime <= now || request.ReviewAgainAt.UtcDateTime > now.AddHours(options.Value.MaxContinuationHours))
            throw new BadRequestException(ErrorCode.InvalidTableSessionReview);
        // İki paralel təsdiq və ya timeout təkrarı ilk menecer qərarını dəyişmir.
        if (!TableSessionCloseReviewPolicy.RequiresReview(session, now))
        {
            if (session.CloseReviewConfirmedByUserId == GetCurrentUserId() &&
                session.CloseReviewDueAt == request.ReviewAgainAt.UtcDateTime && session.CloseReviewReason == request.Reason.Trim())
            {
                await transaction.CommitAsync(cancellationToken);
                return TableSessionViewMapper.Map(session, now);
            }
            throw new BusinessRuleException(ErrorCode.TableSessionReviewRequired);
        }
        session.CloseReviewDueAt = request.ReviewAgainAt.UtcDateTime;
        session.CloseReviewConfirmedAt = now;
        session.CloseReviewConfirmedByUserId = GetCurrentUserId();
        session.CloseReviewReason = request.Reason.Trim();
        session.CloseReviewNotifiedAt = null;
        if (session.Reservation is { } reservation)
            reservation.StatusHistory.Add(new()
            {
                FromStatusId = reservation.StatusId, ToStatusId = reservation.StatusId,
                ChangedAt = now, ChangedByUserId = GetCurrentUserId(),
                Reason = $"{TableSessionReviewTexts.Continued} {session.CloseReviewReason}"
            });
        await sessions.SaveChangesAsync();
        await audit.RecordRestaurantActionAsync(restaurantId, AuditActions.TableSessionServiceContinued,
            new { sessionId, session.CloseReviewDueAt, session.CloseReviewReason }, AuditEntityTypes.TableSession, session.Id);
        await transaction.CommitAsync(cancellationToken);
        return TableSessionViewMapper.Map(session, now);
    }

    public async Task<int> NotifyDueReviewsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await OpenSessions().Where(session => session.CloseReviewNotifiedAt == null &&
                (session.CloseReviewDueAt == null || session.CloseReviewDueAt <= now))
            .OrderBy(session => session.CloseReviewDueAt ?? session.OpenedAt).ThenBy(session => session.Id)
            .Take(batchSize).Select(session => new { session.Id, session.RestaurantId }).ToListAsync(cancellationToken);
        var count = 0;
        foreach (var candidate in candidates)
        {
            await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var session = await LoadLockedAsync(candidate.RestaurantId, candidate.Id, cancellationToken, allowMissing: true);
            if (session == null) { await transaction.CommitAsync(cancellationToken); continue; }
            session.CloseReviewDueAt ??= TableSessionCloseReviewPolicy.GetDueAt(session);
            now = clock.GetUtcNow().UtcDateTime;
            if (TableSessionCloseReviewPolicy.RequiresReview(session, now) && session.CloseReviewNotifiedAt == null)
            {
                var staff = await assignments.GetActiveByRestaurantAndRolesAsync(session.RestaurantId,
                    [(int)RoleCode.Manager, (int)RoleCode.Owner, (int)RoleCode.Waiter]);
                var recipients = staff.Where(member => member.RoleId != (int)RoleCode.Waiter || member.UserId == session.WaiterUserId)
                    .Select(member => member.UserId).Distinct().ToList();
                foreach (var userId in recipients)
                    await notifications.CreateAsync(new CreateNotificationRequest
                    {
                        UserId = userId, RestaurantId = session.RestaurantId,
                        TypeId = (int)NotificationType.TableSessionCloseReviewRequired, ChannelId = (int)NotificationChannel.InApp,
                        Title = TableSessionReviewTexts.RequiredTitle,
                        Message = TableSessionReviewTexts.RequiredMessage(session.Id, session.Table.Name),
                        RelatedEntityType = AuditEntityTypes.TableSession, RelatedEntityId = session.Id,
                        PayloadJson = JsonSerializer.Serialize(new { restaurantId = session.RestaurantId,
                            tableSessionId = session.Id, reservationId = session.ReservationId, section = TableSessionReviewTexts.Section })
                    });
                // Bütün kanallar mövcud notification/outbox mexanizmindən keçir; eyni baxış dövrü yenidən göndərilmir.
                session.CloseReviewNotifiedAt = now;
                if (recipients.Count > 0) count++;
            }
            await sessions.SaveChangesAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        return count;
    }

    /// <summary>Aktiv restoranın bağlanmamış sessiyalarını tarixi və gələcək rezervasiyalara toxunmadan seçir.</summary>
    private IQueryable<Session> OpenSessions() => sessions.Query(session => session.Restaurant.IsActive &&
        session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && session.ClosedAt == null);

    /// <summary>Bir sessiyanın xidmət görünüşü və yerli işgünü hesablanması üçün əlaqələri yükləyir.</summary>
    private static IQueryable<Session> Details(IQueryable<Session> query) => query.Include(session => session.Restaurant)
        .ThenInclude(restaurant => restaurant.WorkingHours).Include(session => session.Table)
        .Include(session => session.CustomerUser).Include(session => session.Reservation);

    /// <summary>Restoran kontekstini və bazadakı aktiv menecer/sahibkar rolunu hər yazmada yenidən yoxlayır.</summary>
    private async Task RequireManagerAsync(int restaurantId)
    {
        if (restaurantId <= 0) throw new BadRequestException(ErrorCode.InvalidRestaurantId);
        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        var role = await assignments.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId);
        if (role != (int)RoleCode.Manager && role != (int)RoleCode.Owner)
            throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
    }

    /// <summary>Əvvəl masa kilidini alır, sonra sessiyanı təzədən oxuyur; bağlama və menecer qərarı ilə paralel bildiriş göndərmir.</summary>
    private async Task<Session?> LoadLockedAsync(int restaurantId, int sessionId, CancellationToken cancellationToken, bool allowMissing = false)
    {
        var tableId = await sessions.Query(session => session.Id == sessionId && session.RestaurantId == restaurantId)
            .Select(session => (int?)session.TableId).SingleOrDefaultAsync(cancellationToken);
        if (tableId.HasValue)
        {
            await tables.AcquireReservationLockAsync(restaurantId, tableId.Value, cancellationToken);
            var session = await Details(sessions.QueryTracked(session => session.Id == sessionId &&
                session.RestaurantId == restaurantId && session.TableId == tableId.Value && session.Restaurant.IsActive &&
                session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && session.ClosedAt == null))
                .SingleOrDefaultAsync(cancellationToken);
            if (session != null) return session;
        }
        if (allowMissing) return null;
        throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }
}
