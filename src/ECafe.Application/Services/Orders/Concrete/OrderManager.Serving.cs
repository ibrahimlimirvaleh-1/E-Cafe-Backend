using System.Data;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Domain.Policies;
using ECafe.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class OrderManager
{
    /// <summary>Cari aktiv ofisiantın açıq sessiyasındakı hazır sifarişi masa kilidi altında bir dəfə təqdim edir; ilk vaxt və audit saxlanılır, maliyyə dəyişmir.</summary>
    public async Task<SessionOrderResponse> MarkServedAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0) throw new BadRequestException(ErrorCode.InvalidOrderId);
        var snapshot = await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        await using var transaction = await _transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await _tables.AcquireReservationLockAsync(restaurantId, snapshot.TableId, cancellationToken);
        var current = await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        if (current.TableId != snapshot.TableId) throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        // İlkin sorğudan sonra sessiya/ofisiant dəyişibsə sifariş həmin ID ilə açıla bilməz; ilkin yaradan ofisianta bağlanmır.
        var order = await QuerySessionOrders(restaurantId, tableSessionId).AsTracking()
            .Where(order => order.Id == orderId && order.TableId == current.TableId && order.Table.RestaurantId == restaurantId)
            .Include(order => order.OrderItems).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.OrderNotFound);
        var fromStatusId = order.StatusId;
        if (OrderServingPolicy.Apply(order, _clock.GetUtcNow().UtcDateTime))
        {
            await _orders.SaveChangesAsync();
            await _audit.RecordRestaurantActionAsync(restaurantId, AuditActions.OrderServed,
                new { orderId, tableSessionId, tableId = order.TableId, servedByUserId = GetCurrentUserId(),
                    servedAt = order.ServedAt, fromStatusId, toStatusId = order.StatusId }, AuditEntityTypes.Order, order.Id);
        }
        await transaction.CommitAsync(cancellationToken);
        return OrderResponseMapper.Map(order);
    }
}
