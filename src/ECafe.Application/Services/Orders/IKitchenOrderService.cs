using ECafe.Application.DTOs.Order;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Orders;

public interface IKitchenOrderService
{
    Task<SessionOrderResponse> SendAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default);
    Task<PaginatedList<KitchenOrderResponse>> GetQueueAsync(int restaurantId, PaginationFilter pagination,
        CancellationToken cancellationToken = default);
}
