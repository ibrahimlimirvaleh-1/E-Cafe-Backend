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

    /// <summary>Qəbul edilmiş sifarişi hazırlanmaya keçirir; ümumi mətbəxdə başqa aktiv əməkdaş da işi davam etdirə bilər.</summary>
    public Task<KitchenOrderResponse> StartPreparingAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default)
        => ProgressAsync(restaurantId, orderId, OrderStatus.Preparing, cancellationToken);

    /// <summary>Hazırlanan sifarişi hazır edir və məsul ofisianta eyni transaction daxilində bir dəfə bildiriş yaradır.</summary>
    public Task<KitchenOrderResponse> MarkReadyAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default)
        => ProgressAsync(restaurantId, orderId, OrderStatus.Ready, cancellationToken);

    /// <summary>Masa kilidindən sonra rol və sifarişi yenidən yoxlayır; status, tarix, əməkdaş, audit və bildiriş birlikdə saxlanılır.</summary>
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
            .Include(order => order.TableSession).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.KitchenOrderNotFound);
        if (order.TableId != tableId) throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        var fromStatusId = order.StatusId;
        if (KitchenOrderProgressPolicy.Apply(order, target, GetCurrentUserId(), clock.GetUtcNow().UtcDateTime))
        {
            await orders.SaveChangesAsync();
            if (target == OrderStatus.Ready) await NotifyResponsibleWaiterAsync(order, cancellationToken);
            await audit.RecordRestaurantActionAsync(restaurantId, ProgressAuditAction(target),
                new { orderId, tableSessionId = order.TableSessionId, tableId, fromStatusId, toStatusId = order.StatusId },
                AuditEntityTypes.Order, order.Id);
        }
        await transaction.CommitAsync(cancellationToken);
        return KitchenOrderResponseMapper.Map(order);
    }

    /// <summary>Hazır sifariş barədə yalnız sessiyanın cari aktiv ofisiantına xəbər verir; başqa işçi və müştəriyə göndərmir.</summary>
    private async Task NotifyResponsibleWaiterAsync(Order order, CancellationToken cancellationToken)
    {
        var waiterId = order.TableSessionId.HasValue
            ? order.TableSession?.RestaurantId == order.RestaurantId ? order.TableSession.WaiterUserId : null
            : order.WaiterUserId;
        if (!waiterId.HasValue || await assignments.GetActiveRoleIdAsync(waiterId.Value, order.RestaurantId) != (int)RoleCode.Waiter)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        await notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = waiterId.Value, RestaurantId = order.RestaurantId, TypeId = (int)NotificationType.OrderReady,
            ChannelId = (int)NotificationChannel.InApp, Title = KitchenOrderTexts.ReadyTitle,
            Message = KitchenOrderTexts.ReadyMessage(order.Id, OrderMappingValues.TableName(order.Table)),
            RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
            PayloadJson = JsonSerializer.Serialize(new { restaurantId = order.RestaurantId, orderId = order.Id,
                tableSessionId = order.TableSessionId, section = KitchenOrderTexts.ReadySection })
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
