using ECafe.Application.DTOs.Order;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IKitchenOrderService
{
    Task<SessionOrderResponse> SendAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default);
    Task<PaginatedList<KitchenOrderResponse>> GetQueueAsync(int restaurantId, PaginationFilter pagination, int? statusId = null,
        CancellationToken cancellationToken = default);
    Task<KitchenOrderResponse> AcceptAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default);
    Task<KitchenOrderResponse> StartPreparingAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default);
    Task<KitchenOrderResponse> MarkReadyAsync(int restaurantId, int orderId, CancellationToken cancellationToken = default);
}
