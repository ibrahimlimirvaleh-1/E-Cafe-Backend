using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Repositories.Order;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.User;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed class CustomerOrderTrackingManager(IHttpContextAccessor accessor, IMapper mapper,
    IConfiguration configuration, ITableSessionRepository sessions, IOrderRepository orders, IUserRepository users)
    : BaseManager(accessor, mapper, configuration), ICustomerOrderTrackingService
{
    public async Task<PaginatedList<CustomerTableSessionResponse>> GetMySessionsAsync(PaginationFilter pagination,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireCustomerAsync(cancellationToken);
        return await ReadPageAsync(CustomerOrderTrackingMapper.ProjectSessions(QueryOwnedSessions(userId)
            .OrderByDescending(session => session.OpenedAt).ThenByDescending(session => session.Id)),
            pagination, cancellationToken);
    }

    public async Task<CustomerTableSessionResponse> GetSessionAsync(int tableSessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireCustomerAsync(cancellationToken);
        ValidateSessionId(tableSessionId);
        return await CustomerOrderTrackingMapper.ProjectSessions(QueryOwnedSessions(userId).Where(session => session.Id == tableSessionId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    public async Task<PaginatedList<CustomerSessionOrderResponse>> GetOrdersAsync(int tableSessionId,
        PaginationFilter pagination, CancellationToken cancellationToken = default)
    {
        var userId = await RequireCustomerAsync(cancellationToken);
        await RequireOwnedSessionAsync(userId, tableSessionId, cancellationToken);
        return await ReadPageAsync(CustomerOrderTrackingMapper.ProjectOrders(QueryOwnedOrders(userId, tableSessionId)
            .OrderByDescending(order => order.OpenedAt).ThenByDescending(order => order.Id)),
            pagination, cancellationToken);
    }

    public async Task<CustomerSessionOrderResponse> GetOrderAsync(int tableSessionId, int orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireCustomerAsync(cancellationToken);
        await RequireOwnedSessionAsync(userId, tableSessionId, cancellationToken);
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        return await CustomerOrderTrackingMapper.ProjectOrders(QueryOwnedOrders(userId, tableSessionId).Where(order => order.Id == orderId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.OrderNotFound);
    }

    /// <summary>Müştəri profilini və hesabın bazadakı aktivliyini yoxlayır; istifadəçi ID-sini sorğudan qəbul etmir.</summary>
    private async Task<int> RequireCustomerAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (GetCurrentRoleId() != (int)RoleCode.Customer ||
            !await users.Query(user => user.Id == userId && user.IsActive).AnyAsync(cancellationToken))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        return userId;
    }

    /// <summary>Sessiyanın cari müştərisini, aktiv hesabını və restoran/masa aidiyyətini hər oxunuşda yoxlayır. Bağlı tarixçə və deaktiv masa/restoran gizlədilmir.</summary>
    private IQueryable<Domain.Entities.TableSession> QueryOwnedSessions(int userId)
        => sessions.Query(session => session.CustomerUserId == userId && session.CustomerUser != null &&
            session.CustomerUser.IsActive && !session.CustomerUser.IsDeleted && !session.Restaurant.IsDeleted &&
            !session.Table.IsDeleted && session.Table.RestaurantId == session.RestaurantId &&
            (!session.ReservationId.HasValue || (session.Reservation != null && !session.Reservation.IsDeleted &&
                session.Reservation.CustomerUserId == userId && session.Reservation.RestaurantId == session.RestaurantId)));

    /// <summary>Sessiyaya giriş hüququnu əvvəlcədən təsdiqləyir; yad və mövcud olmayan ID üçün eyni 404 cavabını saxlayır.</summary>
    private async Task RequireOwnedSessionAsync(int userId, int sessionId, CancellationToken cancellationToken)
    {
        ValidateSessionId(sessionId);
        if (!await QueryOwnedSessions(userId).AnyAsync(session => session.Id == sessionId, cancellationToken))
            throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>Ofisiantın yaratdığı sifarişləri də daxil edir, lakin masa ID-si və ya köhnə sifariş sahibi ilə başqa sessiyaya girişə yol vermir.</summary>
    private IQueryable<Domain.Entities.Order> QueryOwnedOrders(int userId, int sessionId)
    {
        var ownedSessions = QueryOwnedSessions(userId).Where(session => session.Id == sessionId);
        return orders.Query(order => order.TableSessionId == sessionId &&
            (!order.CustomerUserId.HasValue || order.CustomerUserId == userId) &&
            ownedSessions.Any(session => session.Id == order.TableSessionId &&
                session.RestaurantId == order.RestaurantId && session.TableId == order.TableId &&
                (!order.ReservationId.HasValue || order.ReservationId == session.ReservationId)));
    }

    /// <summary>Sıralanmış sorğunu məhdud səhifə ilə icra edir; çox böyük səhifə nömrəsinin tam ədəd daşması yaratmasına imkan vermir.</summary>
    private static async Task<PaginatedList<T>> ReadPageAsync<T>(IQueryable<T> query, PaginationFilter pagination,
        CancellationToken cancellationToken)
    {
        pagination = PaginationFilterNormalizer.Normalize(pagination);
        var count = await query.CountAsync(cancellationToken);
        var skip = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var items = skip >= count ? [] : await query.Skip((int)skip).Take(pagination.PageSize).ToListAsync(cancellationToken);
        return new PaginatedList<T>(items, count, pagination.PageNumber, pagination.PageSize);
    }

    /// <summary>Sessiya ID-si üçün bütün müştəri oxunuşlarında eyni giriş yoxlamasını tətbiq edir.</summary>
    private static void ValidateSessionId(int sessionId)
    {
        if (sessionId <= 0) throw new BadRequestException(ErrorCode.InvalidTableSessionId);
    }

}
