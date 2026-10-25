using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Repositories.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed class OrderCancellationManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IOrderRepository orders, ITableSessionRepository sessions, ITableRepository tables,
    IUserRepository users, IUserRestaurantRepository assignments, IInventoryMovementRepository movements,
    IApplicationDbTransactionFactory transactions, IAuditLogService audit, INotificationService notifications, TimeProvider clock)
    : BaseManager(accessor, mapper, configuration), IOrderCancellationService
{
    /// <summary>Cari aktiv ofisiantın öz masasında hazırlanmamış sifarişi ləğv edir; ilkin yaradana bağlanmır.</summary>
    public async Task<SessionOrderResponse> CancelForWaiterAsync(int restaurantId, int tableSessionId, int orderId,
        CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (restaurantId <= 0) throw new BadRequestException(ErrorCode.InvalidRestaurantId);
        var order = await CancelAsync(restaurantId, tableSessionId, orderId, request, false, cancellationToken);
        return OrderResponseMapper.Map(order);
    }

    /// <summary>Müştərinin yalnız öz masa sessiyasındakı sifarişini ləğv edir; işçi ID-lərini cavaba çıxarmır.</summary>
    public async Task<CustomerSessionOrderResponse> CancelForCustomerAsync(int tableSessionId, int orderId,
        CancelOrderRequest request, CancellationToken cancellationToken = default)
        => CustomerOrderTrackingMapper.Map(await CancelAsync(null, tableSessionId, orderId, request, true, cancellationToken));

    /// <summary>Hazırlama, göndərmə və hesablaşma ilə ortaq masa kilidində aidiyyəti təkrar yoxlayır; ləğv, audit və bildiriş vahid transaction-dadır.</summary>
    private async Task<Order> CancelAsync(int? restaurantId, int sessionId, int orderId, CancelOrderRequest request,
        bool customer, CancellationToken token)
    {
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        var snapshot = await RequireSessionAsync(restaurantId, sessionId, customer, false, token);
        var reason = OrderCancellationPolicy.NormalizeReason(request?.Reason);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, token);
        var session = await RequireSessionAsync(restaurantId, sessionId, customer, true, token);
        if (snapshot.RestaurantId != session.RestaurantId || snapshot.TableId != session.TableId)
            throw new BusinessRuleException(ErrorCode.OrderSessionChanged);
        var order = await orders.QueryTracked(order => order.Id == orderId && order.RestaurantId == session.RestaurantId &&
                order.TableSessionId == session.Id && order.TableId == session.TableId && order.Table.RestaurantId == session.RestaurantId &&
                (!order.ReservationId.HasValue || order.ReservationId == session.ReservationId) &&
                (!customer || !order.CustomerUserId.HasValue || order.CustomerUserId == GetCurrentUserId()))
            .Include(order => order.OrderItems).Include(order => order.Table).Include(order => order.Payments)
            .SingleOrDefaultAsync(token) ?? throw new NotFoundException(ErrorCode.OrderNotFound);
        order.TableSession = session;
        if (OrderCancellationPolicy.IsCancelled(order))
        {
            await transaction.CommitAsync(token);
            return order;
        }
        OrderCancellationPolicy.EnsureCanCancel(order);
        // Yumşaq silinmiş sərfiyyat da qalır: korlanmış köhnə status stoku geri yazmağa və ya izi itirməyə səbəb olmur.
        if (await movements.Query().IgnoreQueryFilters().AnyAsync(movement => movement.RelatedOrderId == order.Id &&
                movement.RestaurantId == order.RestaurantId &&
                movement.MovementTypeId == (int)InventoryMovementTypeCode.OrderConsumption, token))
            throw new BusinessRuleException(ErrorCode.OrderCancellationStockConflict);
        var fromStatusId = order.StatusId;
        var now = clock.GetUtcNow().UtcDateTime;
        var storedUtc = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
        OrderCancellationPolicy.Apply(order, GetCurrentUserId(), storedUtc, reason);
        await orders.SaveChangesAsync();
        await audit.RecordRestaurantActionAsync(order.RestaurantId, AuditActions.OrderCancelled,
            new { orderId, tableSessionId = sessionId, tableId = order.TableId, fromStatusId, toStatusId = order.StatusId,
                cancelledAt = order.CancelledAt, cancelledByUserId = order.CancelledByUserId, reason }, AuditEntityTypes.Order, order.Id);
        await NotifyAsync(order, session, token);
        await transaction.CommitAsync(token);
        return order;
    }

    /// <summary>İstifadəçi aktivliyini, canlı ofisiant rolunu və sessiyanın masa/restoran/müştəri aidiyyətini hər kilid tərəfində yenidən yoxlayır.</summary>
    private async Task<TableSession> RequireSessionAsync(int? restaurantId, int sessionId, bool customer, bool tracked, CancellationToken token)
    {
        if (sessionId <= 0) throw new BadRequestException(ErrorCode.InvalidTableSessionId);
        var userId = GetCurrentUserId();
        if (!await users.Query(user => user.Id == userId && user.IsActive).AnyAsync(token))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (customer && GetCurrentRoleId() != (int)RoleCode.Customer)
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (!customer)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId!.Value);
            if (await assignments.GetActiveRoleIdAsync(userId, restaurantId.Value) != (int)RoleCode.Waiter)
                throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
        }
        var query = tracked ? sessions.QueryTracked() : sessions.Query();
        return await query.Where(session => session.Id == sessionId &&
                (!restaurantId.HasValue || session.RestaurantId == restaurantId) &&
                session.Table.RestaurantId == session.RestaurantId && !session.Table.IsDeleted && !session.Restaurant.IsDeleted &&
                (customer ? session.CustomerUserId == userId : session.WaiterUserId == userId) &&
                (!session.ReservationId.HasValue || session.Reservation != null && !session.Reservation.IsDeleted &&
                    session.Reservation.RestaurantId == session.RestaurantId &&
                    (!customer || session.Reservation.CustomerUserId == userId)))
            .Include(session => session.Payments).SingleOrDefaultAsync(token)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>Müştəri və cari ofisiantı, göndərilmiş sifarişdə isə aktiv mətbəxi xəbərdar edir; səbəbin sərbəst mətnini bildirişdə paylaşmır.</summary>
    private async Task NotifyAsync(Order order, TableSession session, CancellationToken token)
    {
        var recipients = new HashSet<int>();
        if (session.CustomerUserId.HasValue && await users.Query(user => user.Id == session.CustomerUserId && user.IsActive).AnyAsync(token))
            recipients.Add(session.CustomerUserId.Value);
        if (session.WaiterUserId.HasValue && await assignments.GetActiveRoleIdAsync(session.WaiterUserId.Value, order.RestaurantId) == (int)RoleCode.Waiter)
            recipients.Add(session.WaiterUserId.Value);
        if (order.SentToKitchenAt.HasValue)
            foreach (var staff in await assignments.GetActiveByRestaurantAndRolesAsync(order.RestaurantId, [(int)RoleCode.Kitchen]))
                recipients.Add(staff.UserId);
        recipients.Remove(GetCurrentUserId());
        foreach (var recipient in recipients)
        {
            token.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = recipient, RestaurantId = order.RestaurantId, TypeId = (int)NotificationType.OrderCancelled,
                ChannelId = (int)NotificationChannel.InApp, Title = OrderCancellationTexts.Title,
                Message = OrderCancellationTexts.Message(order.Id, OrderMappingValues.TableName(order.Table)),
                RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
                PayloadJson = JsonSerializer.Serialize(new { orderId = order.Id, restaurantId = order.RestaurantId,
                    tableSessionId = session.Id, section = OrderCancellationTexts.Section })
            });
        }
    }
}
