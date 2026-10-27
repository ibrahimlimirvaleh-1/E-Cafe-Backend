using System.Data;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
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

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class OrderCancellationRequestManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IOrderRepository orders, ITableSessionRepository sessions, ITableRepository tables, IUserRepository users,
    IUserRestaurantRepository assignments, IApplicationDbTransactionFactory transactions, IAuditLogService audit,
    INotificationService notifications, TimeProvider clock, IOrderStockReturnService stockReturn)
    : BaseManager(accessor, mapper, configuration), IOrderCancellationRequestService
{
    /// <summary>Ofisiantın hazırlanan sifariş üçün yaratdığı sorğunu dərhal mətbəx qərarına təqdim edir.</summary>
    public async Task<SessionOrderResponse> RequestForWaiterAsync(int restaurantId, int sessionId, int orderId,
        CancelOrderRequest request, CancellationToken token = default)
        => OrderResponseMapper.Map(await MutateOwnedAsync(restaurantId, sessionId, orderId, false, false, request?.Reason, token));

    /// <summary>Müştərinin öz hazırlanan sifarişinə sorğu yaradır; mətbəx qərarından əvvəl cari ofisianta çatdırır.</summary>
    public async Task<CustomerSessionOrderResponse> RequestForCustomerAsync(int sessionId, int orderId,
        CancelOrderRequest request, CancellationToken token = default)
        => CustomerOrderTrackingMapper.Map(await MutateOwnedAsync(null, sessionId, orderId, true, false, request?.Reason, token));

    /// <summary>Müştəri istəyini yalnız cari məsul ofisiant eyni səbəblə mətbəxə ötürür.</summary>
    public async Task<SessionOrderResponse> ForwardAsync(int restaurantId, int sessionId, int orderId, CancellationToken token = default)
        => OrderResponseMapper.Map(await MutateOwnedAsync(restaurantId, sessionId, orderId, false, true, null, token));

    /// <summary>Mətbəxin qeyri-müəyyən POST nəticəsini yoxlamaq üçün hətta ləğv edilmiş sifarişin təhlükəsiz görünüşünü qaytarır.</summary>
    public async Task<KitchenOrderResponse> GetForKitchenAsync(int restaurantId, int orderId, CancellationToken token = default)
    {
        await RequireKitchenAsync(restaurantId, token);
        var order = await KitchenOrderQuery(restaurantId, orderId, false).SingleOrDefaultAsync(token)
            ?? throw new NotFoundException(ErrorCode.KitchenOrderNotFound);
        if (order.CancellationRequest == null) throw new NotFoundException(ErrorCode.OrderCancellationRequestNotFound);
        return KitchenOrderResponseMapper.Map(order);
    }

    /// <summary>Mətbəx qərarını ortaq masa kilidində tətbiq edir; istifadə olunmamış ərzaq ilkin uçotdan atomik qaytarılır, ödənişə toxunmur.</summary>
    public async Task<KitchenOrderResponse> DecideAsync(int restaurantId, int orderId, DecideOrderCancellationRequest request,
        CancellationToken token = default)
    {
        await RequireKitchenAsync(restaurantId, token);
        if (request == null) throw new BadRequestException(ErrorCode.RequestCannotBeNull);
        if (request.Approved is not bool approved) throw new BadRequestException(ErrorCode.InvalidOrderRequest);
        if (!approved && request.IngredientsUnused == true) throw new BadRequestException(ErrorCode.InvalidOrderRequest);
        var reason = OrderCancellationPolicy.NormalizeReason(request.Reason);
        var tableId = await KitchenOrderQuery(restaurantId, orderId, false).Select(order => order.TableId).SingleOrDefaultAsync(token);
        if (tableId <= 0) throw new NotFoundException(ErrorCode.KitchenOrderNotFound);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await tables.AcquireReservationLockAsync(restaurantId, tableId, token);
        await RequireKitchenAsync(restaurantId, token);
        var order = await KitchenOrderQuery(restaurantId, orderId, true).SingleOrDefaultAsync(token)
            ?? throw new NotFoundException(ErrorCode.KitchenOrderNotFound);
        if (order.TableId != tableId) throw new BusinessRuleException(ErrorCode.OrderSessionChanged);
        var changed = OrderCancellationRequestPolicy.Decide(order, approved, GetCurrentUserId(), UtcNow(), reason, request.IngredientsUnused ?? false);
        if (changed && approved && request.IngredientsUnused == true)
        {
            await stockReturn.RestoreAsync(order, token);
            order.CancellationRequest!.StockReturnedAt = order.CancellationRequest.ResolvedAt;
        }
        if (changed) await SaveDecisionAsync(order, approved ? AuditActions.OrderCancellationApproved : AuditActions.OrderCancellationRejected, token);
        await transaction.CommitAsync(token);
        return KitchenOrderResponseMapper.Map(order);
    }

    /// <summary>Kilidin hər iki tərəfində sessiya və canlı aidiyyəti yoxlayır; bir sifariş üçün bir sorğunu və ilk səbəbi saxlayır.</summary>
    private async Task<Order> MutateOwnedAsync(int? restaurantId, int sessionId, int orderId, bool customer, bool forward,
        string? rawReason, CancellationToken token)
    {
        var snapshot = await RequireSessionAsync(restaurantId, sessionId, customer, false, token);
        var reason = forward ? null : OrderCancellationPolicy.NormalizeReason(rawReason);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, token);
        var session = await RequireSessionAsync(restaurantId, sessionId, customer, true, token);
        if (session.RestaurantId != snapshot.RestaurantId || session.TableId != snapshot.TableId)
            throw new BusinessRuleException(ErrorCode.OrderSessionChanged);
        var order = await orders.QueryTracked(order => order.Id == orderId && order.RestaurantId == session.RestaurantId &&
                order.TableSessionId == session.Id && order.TableId == session.TableId && order.Table.RestaurantId == session.RestaurantId &&
                (!order.ReservationId.HasValue || order.ReservationId == session.ReservationId) &&
                (!customer || !order.CustomerUserId.HasValue || order.CustomerUserId == GetCurrentUserId()))
            .Include(order => order.CancellationRequest).Include(order => order.Payments).Include(order => order.OrderItems)
            .Include(order => order.Table).Include(order => order.Restaurant).SingleOrDefaultAsync(token)
            ?? throw new NotFoundException(ErrorCode.OrderNotFound);
        order.TableSession = session;
        var isNew = order.CancellationRequest == null;
        if (isNew && customer && (!session.WaiterUserId.HasValue ||
            await assignments.GetActiveRoleIdAsync(session.WaiterUserId.Value, session.RestaurantId) != (int)RoleCode.Waiter))
            throw new BusinessRuleException(ErrorCode.OrderCancellationWaiterUnavailable);
        var changed = forward
            ? OrderCancellationRequestPolicy.Forward(order, GetCurrentUserId(), UtcNow())
            : OrderCancellationRequestPolicy.Request(order, customer, GetCurrentUserId(), UtcNow(), reason!);
        if (changed) await SaveDecisionAsync(order, forward ? AuditActions.OrderCancellationForwarded : AuditActions.OrderCancellationRequested, token);
        await transaction.CommitAsync(token);
        return order;
    }

    /// <summary>Sorğu, audit və çıxış bildirişi eyni transaction-da saxlanılır; təkrarda ikinci tarix/audit yaranmır.</summary>
    private async Task SaveDecisionAsync(Order order, string action, CancellationToken token)
    {
        await orders.SaveChangesAsync();
        var request = order.CancellationRequest!;
        await audit.RecordRestaurantActionAsync(order.RestaurantId, action,
            new { orderId = order.Id, tableSessionId = order.TableSessionId, requestId = request.Id,
                request.StatusId, request.Reason, request.RequestedAt, request.ForwardedAt, request.ResolvedAt,
                request.DecisionReason, request.IngredientsUnused, request.StockReturnedAt, actorId = GetCurrentUserId() }, AuditEntityTypes.Order, order.Id);
        await NotifyAsync(order, token);
    }

    /// <summary>PostgreSQL-in mikrosaniyə dəqiqliyini saxlayır ki, ilk və təkrar cavabın vaxtları eyni olsun.</summary>
    private DateTime UtcNow()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
    }
}
