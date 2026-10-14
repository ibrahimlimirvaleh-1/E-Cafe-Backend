using ECafe.Application.DTOs.Order;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Orders;

public interface IOrderService
{
    Task<CreateOrderResult> CreateForWaiterAsync(int restaurantId, int tableSessionId, Guid requestId,
        CreateWaiterOrderRequest request, CancellationToken cancellationToken = default);
    Task<PaginatedList<SessionOrderResponse>> GetForSessionAsync(int restaurantId, int tableSessionId,
        PaginationFilter pagination, CancellationToken cancellationToken = default);
    Task<SessionOrderResponse> GetByIdAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default);
}
