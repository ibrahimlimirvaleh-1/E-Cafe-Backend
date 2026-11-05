using System.Data;
using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class KitchenOrderManager
{
    /// <summary>Göndərilmiş sifarişin ilk mətbəx qəbulunu qeydə alır; digər sifarişlərin qəbulunu məhdudlaşdırmır.</summary>
    public Task<KitchenOrderResponse> AcceptAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default)
        => ProgressAsync(restaurantId, orderId, OrderStatus.Accepted, cancellationToken);

    /// <summary>Qəbul edilmiş sifarişi yalnız tam resept və kifayət edən stok olduqda hazırlanmaya keçirir; inqrediyentlər transaction daxilində bir dəfə silinir.</summary>
    public Task<KitchenOrderResponse> StartPreparingAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default)
        => ProgressAsync(restaurantId, orderId, OrderStatus.Preparing, cancellationToken);

    /// <summary>Hazırlanan sifarişi hazır edir; məsul ofisiant və sifarişin müştərisi üçün eyni transaction daxilində bildiriş yaradır.</summary>
    public Task<KitchenOrderResponse> MarkReadyAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default)
        => ProgressAsync(restaurantId, orderId, OrderStatus.Ready, cancellationToken);

    /// <summary>Masa kilidindən sonra rol və sifarişi yenidən yoxlayır; hazırlama istehlakı, status, tarix, audit və bildiriş birlikdə saxlanılır.</summary>
    private async Task<KitchenOrderResponse> ProgressAsync(int restaurantId, int orderId, OrderStatus target,
        CancellationToken cancellationToken)
    {
        await RequireKitchenAsync(restaurantId);
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        var tableId = await orders.Query(order => order.Id == orderId && order.RestaurantId == restaurantId)
            .Select(order => (int?)order.TableId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.KitchenOrderNotFound);

        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await tables.AcquireReservationLockAsync(restaurantId, tableId, cancellationToken);
        await RequireKitchenAsync(restaurantId);
        var order = await orders.QueryTracked(order => order.Id == orderId && order.RestaurantId == restaurantId &&
                order.Table.RestaurantId == restaurantId)
            .Include(order => order.OrderItems).Include(order => order.Table).Include(order => order.Restaurant)
            .Include(order => order.CancellationRequest).Include(order => order.Payments)
            .Include(order => order.TableSession).ThenInclude(session => session!.Payments).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.KitchenOrderNotFound);
        if (order.TableId != tableId) throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        var fromStatusId = order.StatusId;
        if (target == OrderStatus.Preparing && KitchenOrderProgressPolicy.CanApply(order, target))
            await stock.ConsumeAsync(order, cancellationToken);
        // PostgreSQL mikrosaniyə saxlayır; ilk cavabın vaxtı təkrar sorğudakı DB vaxtı ilə eyni dəqiqlikdə qalır.
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var storedUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
        if (target == OrderStatus.Ready && KitchenOrderProgressPolicy.CanApply(order, target) &&
            OrderCancellationRequestPolicy.ExpireAwaitingWaiter(order, storedUtc, OrderCancellationRequestTexts.ExpiredReason))
        {
            await audit.RecordRestaurantActionAsync(restaurantId, AuditActions.OrderCancellationExpired,
                new { orderId, requestId = order.CancellationRequest!.Id }, AuditEntityTypes.Order, orderId);
            await NotifyExpiredCancellationAsync(order, cancellationToken);
        }
        if (KitchenOrderProgressPolicy.Apply(order, target, GetCurrentUserId(), storedUtc))
        {
            await orders.SaveChangesAsync();
            await NotifyResponsibleWaiterAsync(order, target, cancellationToken);
            await NotifyCustomerProgressAsync(order, target, cancellationToken);
            await audit.RecordRestaurantActionAsync(restaurantId, ProgressAuditAction(target),
                new { orderId, tableSessionId = order.TableSessionId, tableId, fromStatusId, toStatusId = order.StatusId },
                AuditEntityTypes.Order, order.Id);
        }
        await transaction.CommitAsync(cancellationToken);
        return KitchenOrderResponseMapper.Map(order);
    }

    /// <summary>Yalnız açıq sessiya ilə sifarişdə ortaq olan aktiv müştəriyə yeni mətbəx mərhələsini bildirir.</summary>
    private async Task NotifyCustomerProgressAsync(Order order, OrderStatus target, CancellationToken cancellationToken)
    {
        if (order.CustomerUserId is not > 0 || order.TableSessionId is not > 0)
            return;

        var customerId = order.CustomerUserId.Value;
        var isOwned = await orders.Query(candidate =>
                candidate.Id == order.Id && candidate.RestaurantId == order.RestaurantId &&
                candidate.CustomerUserId == customerId && candidate.CustomerUser != null &&
                candidate.CustomerUser.IsActive && !candidate.CustomerUser.IsDeleted &&
                candidate.CustomerUser.RoleId == (int)RoleCode.Customer &&
                candidate.TableSession != null && !candidate.TableSession.IsDeleted &&
                candidate.TableSession.ClosedAt == null && candidate.TableSession.CustomerUserId == customerId &&
                candidate.TableSession.RestaurantId == candidate.RestaurantId && candidate.TableSession.TableId == candidate.TableId &&
                (!candidate.ReservationId.HasValue || candidate.ReservationId == candidate.TableSession.ReservationId) &&
                (!candidate.TableSession.ReservationId.HasValue ||
                 candidate.TableSession.Reservation != null && !candidate.TableSession.Reservation.IsDeleted &&
                 candidate.TableSession.Reservation.CustomerUserId == customerId &&
                 candidate.TableSession.Reservation.RestaurantId == candidate.RestaurantId))
            .AnyAsync(cancellationToken);
        if (!isOwned) return;

        var notice = KitchenOrderTexts.CustomerProgress(target, order.Id);
        await notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = customerId, RestaurantId = order.RestaurantId, TypeId = (int)notice.Type,
            ChannelId = (int)NotificationChannel.InApp, Title = notice.Title, Message = notice.Message,
            RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
            PayloadJson = JsonSerializer.Serialize(new { orderId = order.Id, restaurantId = order.RestaurantId,
                tableSessionId = order.TableSessionId, section = KitchenOrderTexts.ProgressSection })
        });
    }

    /// <summary>Mətbəx mərhələsini yalnız masanın cari aktiv ofisiantına bildirir; sessiyasız sifarişdə təyin edilmiş ofisiantı yoxlayır.</summary>
    private async Task NotifyResponsibleWaiterAsync(Order order, OrderStatus target, CancellationToken cancellationToken)
    {
        var waiterId = order.TableSessionId.HasValue
            ? order.TableSession is { IsDeleted: false } session &&
                session.RestaurantId == order.RestaurantId && session.TableId == order.TableId ? session.WaiterUserId : null
            : order.WaiterUserId;
        if (!waiterId.HasValue || await assignments.GetActiveRoleIdAsync(waiterId.Value, order.RestaurantId) != (int)RoleCode.Waiter)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        var notice = KitchenOrderTexts.WaiterProgress(target, order.Id, OrderMappingValues.TableName(order.Table));
        var section = target == OrderStatus.Ready ? KitchenOrderTexts.ReadySection : KitchenOrderTexts.ProgressSection;
        await notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = waiterId.Value, RestaurantId = order.RestaurantId, TypeId = (int)notice.Type,
            ChannelId = (int)NotificationChannel.InApp, Title = notice.Title, Message = notice.Message,
            RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
            PayloadJson = JsonSerializer.Serialize(new { restaurantId = order.RestaurantId, orderId = order.Id,
                tableSessionId = order.TableSessionId, section })
        });
    }

    /// <summary>Mətbəxə çatmamış müştəri sorğusunun hazır mərhələsində köhnəldiyini eyni transaction-da bildirir.</summary>
    private async Task NotifyExpiredCancellationAsync(Order order, CancellationToken token)
    {
        if (order.TableSession?.CustomerUserId is not int customerId) return;
        token.ThrowIfCancellationRequested();
        await notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = customerId, RestaurantId = order.RestaurantId,
            TypeId = (int)NotificationType.OrderCancellationRequestUpdated, ChannelId = (int)NotificationChannel.InApp,
            Title = OrderCancellationRequestTexts.Title,
            Message = OrderCancellationRequestTexts.Message(order.Id, OrderCancellationRequestStatus.Expired),
            RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
            PayloadJson = JsonSerializer.Serialize(new { orderId = order.Id, restaurantId = order.RestaurantId,
                tableSessionId = order.TableSessionId, section = OrderCancellationRequestTexts.Section })
        });
    }

    /// <summary>Mətbəx mərhələsini mərkəzləşdirilmiş audit koduna çevirərək hər uğurlu keçidi ayrıca izlənən edir.</summary>
    private static string ProgressAuditAction(OrderStatus target) => target switch
    {
        OrderStatus.Accepted => AuditActions.OrderAcceptedByKitchen,
        OrderStatus.Preparing => AuditActions.OrderPreparationStarted,
        OrderStatus.Ready => AuditActions.OrderMarkedReady,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };
}
