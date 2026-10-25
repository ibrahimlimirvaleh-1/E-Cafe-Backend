using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Mappings;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class OrderCancellationRequestManager
{
    /// <summary>Aktiv istifadəçi, tokenin müştəri rolu və ya canlı ofisiant təyinatı ilə şəxsi sessiya aidiyyətini yoxlayır.</summary>
    private async Task<TableSession> RequireSessionAsync(int? restaurantId, int sessionId, bool customer, bool tracked, CancellationToken token)
    {
        if (sessionId <= 0) throw new BadRequestException(ErrorCode.InvalidTableSessionId);
        var userId = GetCurrentUserId();
        await RequireActiveUserAsync(token);
        if (customer && GetCurrentRoleId() != (int)RoleCode.Customer) throw new ForbiddenException(ErrorCode.AccessDenied);
        if (!customer)
        {
            if (restaurantId is null or <= 0) throw new BadRequestException(ErrorCode.InvalidRestaurantId);
            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);
            if (await assignments.GetActiveRoleIdAsync(userId, restaurantId.Value) != (int)RoleCode.Waiter)
                throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
        }
        var query = tracked ? sessions.QueryTracked() : sessions.Query();
        return await query.Where(session => session.Id == sessionId &&
                (!restaurantId.HasValue || session.RestaurantId == restaurantId) && !session.Table.IsDeleted &&
                !session.Restaurant.IsDeleted && session.Table.RestaurantId == session.RestaurantId &&
                (customer ? session.CustomerUserId == userId : session.WaiterUserId == userId) &&
                (!session.ReservationId.HasValue || session.Reservation != null && !session.Reservation.IsDeleted &&
                    session.Reservation.RestaurantId == session.RestaurantId && (!customer || session.Reservation.CustomerUserId == userId)))
            .Include(session => session.Payments).SingleOrDefaultAsync(token)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>Ümumi sifariş icazəsi deyil, aktiv istifadəçi və həmin restoranın canlı mətbəx rolu tələb olunur.</summary>
    private async Task RequireKitchenAsync(int restaurantId, CancellationToken token)
    {
        if (restaurantId <= 0) throw new BadRequestException(ErrorCode.InvalidRestaurantId);
        await RequireActiveUserAsync(token);
        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        if (await assignments.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId) != (int)RoleCode.Kitchen)
            throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
    }

    /// <summary>Deaktiv və silinmiş hesabların köhnə tokenlə qərar verməsini dayandırır.</summary>
    private async Task RequireActiveUserAsync(CancellationToken token)
    {
        if (!await users.Query(user => user.Id == GetCurrentUserId() && user.IsActive).AnyAsync(token))
            throw new ForbiddenException(ErrorCode.AccessDenied);
    }

    /// <summary>Mətbəxin sorğu görünüşündə restoran, masa və sessiya sərhədlərini saxlayır; hazır qiymətlər cavaba çıxarılmır.</summary>
    private IQueryable<Order> KitchenOrderQuery(int restaurantId, int orderId, bool tracked)
    {
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        var query = tracked ? orders.QueryTracked() : orders.Query();
        return query.Where(order => order.Id == orderId && order.RestaurantId == restaurantId &&
                !order.Restaurant.IsDeleted && !order.Table.IsDeleted && order.Table.RestaurantId == restaurantId &&
                order.SentToKitchenAt != null && order.TableSession != null && !order.TableSession.IsDeleted &&
                order.TableSession.RestaurantId == restaurantId && order.TableSession.TableId == order.TableId &&
                (!order.ReservationId.HasValue || order.ReservationId == order.TableSession.ReservationId))
            .Include(order => order.TableSession).ThenInclude(session => session!.Payments)
            .Include(order => order.Payments).Include(order => order.CancellationRequest).Include(order => order.OrderItems)
            .Include(order => order.Table).Include(order => order.Restaurant);
    }

    /// <summary>Sərbəst səbəbi push mətninə çıxarmadan, mərhələyə uyğun ofisiant/müştəri/mətbəxə mövcud outbox yolundan xəbər verir.</summary>
    private async Task NotifyAsync(Order order, CancellationToken token)
    {
        var state = order.CancellationRequest!.StatusId;
        var recipients = new HashSet<int>();
        var session = order.TableSession!;
        if (session.WaiterUserId.HasValue && await assignments.GetActiveRoleIdAsync(session.WaiterUserId.Value, order.RestaurantId) == (int)RoleCode.Waiter)
            recipients.Add(session.WaiterUserId.Value);
        if (state != OrderCancellationRequestStatus.AwaitingWaiter)
        {
            if (session.CustomerUserId.HasValue) recipients.Add(session.CustomerUserId.Value);
            foreach (var staff in await assignments.GetActiveByRestaurantAndRolesAsync(order.RestaurantId, [(int)RoleCode.Kitchen]))
                recipients.Add(staff.UserId);
        }
        recipients.Remove(GetCurrentUserId());
        var active = await users.Query(user => recipients.Contains(user.Id) && user.IsActive).Select(user => user.Id).ToListAsync(token);
        foreach (var recipient in active)
        {
            token.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = recipient, RestaurantId = order.RestaurantId, TypeId = (int)NotificationType.OrderCancellationRequestUpdated,
                ChannelId = (int)NotificationChannel.InApp, Title = OrderCancellationRequestTexts.Title,
                Message = OrderCancellationRequestTexts.Message(order.Id, state), RelatedEntityType = AuditEntityTypes.Order,
                RelatedEntityId = order.Id, PayloadJson = JsonSerializer.Serialize(new { orderId = order.Id,
                    restaurantId = order.RestaurantId, tableSessionId = order.TableSessionId, section = OrderCancellationRequestTexts.Section })
            });
        }
    }
}
