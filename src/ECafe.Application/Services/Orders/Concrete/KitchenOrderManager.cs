using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Repositories.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OrderEntity = ECafe.Domain.Entities.Order;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class KitchenOrderManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IOrderRepository orders, ITableSessionService sessionAccess, ITableRepository tables,
    IUserRestaurantRepository assignments, IApplicationDbTransactionFactory transactions,
    INotificationService notifications, IAuditLogService audit, TimeProvider clock, IOrderStockService stock)
    : BaseManager(accessor, mapper, configuration), IKitchenOrderService
{
    /// <summary>Cari ofisiantın sifarişini masa kilidində bir dəfə göndərir; müştəri sifarişinin təsdiqini göndərmə ilə atomik saxlayır, stok və ödəniş dəyişmir.</summary>
    public async Task<SessionOrderResponse> SendAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        var snapshot = await sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await tables.AcquireReservationLockAsync(restaurantId, snapshot.TableId, cancellationToken);
        var current = await sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        if (current.TableId != snapshot.TableId) throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        var order = await orders.QueryTracked(order => order.Id == orderId && order.RestaurantId == restaurantId &&
                order.TableSessionId == tableSessionId && order.TableId == current.TableId &&
                (order.WaiterUserId == GetCurrentUserId() && order.SourceId == (int)OrderSource.Waiter ||
                 order.SourceId == (int)OrderSource.Customer && order.CustomerUserId.HasValue &&
                 order.TableSession != null && order.CustomerUserId == order.TableSession.CustomerUserId))
            .Include(order => order.OrderItems).Include(order => order.Table).Include(order => order.Payments)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.OrderNotFound);

        // Eyni sifarişin ID-si təbii təkrar sorğu açarıdır; tarix, audit və bildiriş ikinci dəfə yaradılmır.
        if (order.SentToKitchenAt.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return OrderResponseMapper.Map(order, await orders.Query(candidate => candidate.Id == order.Id)
                .Where(OrderCancellationPolicy.Cancellable).AnyAsync(cancellationToken));
        }
        if (order.SourceId == (int)OrderSource.Customer)
        {
            if (!CustomerOrderSubmissionPolicy.CanApprove(order) ||
                !await CanApproveCustomerOrderAsync(order.Id, cancellationToken))
                throw new BusinessRuleException(ErrorCode.OrderCannotSendToKitchen);
            order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Created);
            order.WaiterUserId = GetCurrentUserId();
        }
        if (order.ClosedAt != null || order.StatusId != StatusIds.Build(StatusType.Order, (int)OrderStatus.Created) ||
            !order.OrderItems.Any(line => !line.IsDeleted) || order.OrderItems.Any(line => !line.IsDeleted && line.Quantity <= 0))
            throw new BusinessRuleException(ErrorCode.OrderCannotSendToKitchen);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        order.SentToKitchenAt = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
        await orders.SaveChangesAsync();
        await NotifyKitchenAsync(order, cancellationToken);
        await audit.RecordRestaurantActionAsync(restaurantId, AuditActions.OrderSentToKitchen,
            new { orderId, tableSessionId, tableId = order.TableId }, AuditEntityTypes.Order, orderId);
        await transaction.CommitAsync(cancellationToken);
        return OrderResponseMapper.Map(order, await orders.Query(candidate => candidate.Id == order.Id)
            .Where(OrderCancellationPolicy.Cancellable).AnyAsync(cancellationToken));
    }

    /// <summary>Müştəri sifarişinin təsdiqində sessiya/müqavilə/ödəniş və menyu mövcudluğunu yenidən SQL-də yoxlayır; qəbul edilmiş qiymət nüsxəsini dəyişmir.</summary>
    private Task<bool> CanApproveCustomerOrderAsync(int orderId, CancellationToken cancellationToken)
        => orders.Query(order => order.Id == orderId && order.TableSession != null &&
                order.OrderItems.All(line => line.IsDeleted || !line.Item.IsDeleted && line.Item.IsActive &&
                    line.Item.IsAvailable && line.Item.RestaurantId == order.RestaurantId &&
                    line.Item.Category.IsActive && !line.Item.Category.IsDeleted &&
                    line.Item.Category.RestaurantId == order.RestaurantId &&
                    (line.Item.StatusId == StatusIds.Item(ItemStatus.Available) || line.Item.StatusId == StatusIds.Item(ItemStatus.Limited))))
            .Select(order => order.TableSession!)
            .Where(CustomerOrderSubmissionPolicy.AvailableAt(clock.GetUtcNow().UtcDateTime))
            .AnyAsync(cancellationToken);

    /// <summary>Aktiv mətbəx işçisinə göndərilmiş açıq sifarişləri mərhələyə görə səhifələmədən əvvəl süzür və FIFO ilə qaytarır; maliyyə və müştəri məlumatı çıxarmır.</summary>
    public async Task<PaginatedList<KitchenOrderResponse>> GetQueueAsync(int restaurantId, PaginationFilter pagination, int? statusId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireKitchenAsync(restaurantId);
        if (statusId.HasValue &&
            statusId != StatusIds.Build(StatusType.Order, (int)OrderStatus.Created) &&
            statusId != StatusIds.Build(StatusType.Order, (int)OrderStatus.Accepted) &&
            statusId != StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing) &&
            statusId != StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready))
            throw new BadRequestException(ErrorCode.InvalidOrderRequest);
        pagination = PaginationFilterNormalizer.Normalize(pagination);
        var query = orders.Query(order => order.RestaurantId == restaurantId && order.Restaurant.IsActive &&
            order.Table.RestaurantId == restaurantId &&
            order.SentToKitchenAt != null && order.ClosedAt == null &&
            (order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Created) ||
             order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Accepted) ||
             order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing) ||
             order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready)));
        if (statusId.HasValue) query = query.Where(order => order.StatusId == statusId.Value);
        var count = await query.CountAsync(cancellationToken);
        var skip = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        // Sessiyanın bağlanması artıq mətbəxə çatmış sifarişi səssiz gizlətməməlidir.
        var page = skip >= count ? [] : await query.OrderBy(order => order.SentToKitchenAt).ThenBy(order => order.Id)
            .Skip((int)skip).Take(pagination.PageSize).Include(order => order.Table)
            .Include(order => order.OrderItems).Include(order => order.Restaurant).Include(order => order.CancellationRequest).ToListAsync(cancellationToken);
        var ids = page.Select(order => order.Id).ToArray();
        var eligibleIds = await orders.Query(order => ids.Contains(order.Id)).Where(OrderCancellationRequestPolicy.Eligible)
            .Select(order => order.Id).ToListAsync(cancellationToken);
        return new(page.Select(order => KitchenOrderResponseMapper.Map(order, eligibleIds.Contains(order.Id) &&
            order.CancellationRequest?.StatusId == OrderCancellationRequestStatus.PendingKitchen)).ToList(), count, pagination.PageNumber, pagination.PageSize);
    }

    /// <summary>Token kontekstindən əlavə bazadakı aktiv mətbəx təyinatını yoxlayır; başqa rol və restoranın növbəsini açmır.</summary>
    private async Task RequireKitchenAsync(int restaurantId)
    {
        if (restaurantId <= 0) throw new BadRequestException(ErrorCode.InvalidRestaurantId);
        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        if (await assignments.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId) != (int)RoleCode.Kitchen)
            throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
    }

    /// <summary>Yalnız həmin restoranın aktiv mətbəx işçilərinə mövcud bildiriş/outbox yolu ilə eyni transaction-da xəbər verir.</summary>
    private async Task NotifyKitchenAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        var staff = await assignments.GetActiveByRestaurantAndRolesAsync(order.RestaurantId, [(int)RoleCode.Kitchen]);
        foreach (var userId in staff.Select(member => member.UserId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId, RestaurantId = order.RestaurantId,
                TypeId = (int)NotificationType.OrderCreated, ChannelId = (int)NotificationChannel.InApp,
                Title = KitchenOrderTexts.NotificationTitle,
                Message = KitchenOrderTexts.SentMessage(order.Id, OrderMappingValues.TableName(order.Table)),
                RelatedEntityType = AuditEntityTypes.Order, RelatedEntityId = order.Id,
                PayloadJson = JsonSerializer.Serialize(new { restaurantId = order.RestaurantId, orderId = order.Id,
                    tableSessionId = order.TableSessionId, section = KitchenOrderTexts.Section })
            });
        }
    }

}
