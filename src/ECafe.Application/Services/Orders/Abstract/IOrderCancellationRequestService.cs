using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IOrderCancellationRequestService
{
    Task<SessionOrderResponse> RequestForWaiterAsync(int restaurantId, int sessionId, int orderId, CancelOrderRequest request, CancellationToken token = default);
    Task<CustomerSessionOrderResponse> RequestForCustomerAsync(int sessionId, int orderId, CancelOrderRequest request, CancellationToken token = default);
    Task<SessionOrderResponse> ForwardAsync(int restaurantId, int sessionId, int orderId, CancellationToken token = default);
    Task<KitchenOrderResponse> GetForKitchenAsync(int restaurantId, int orderId, CancellationToken token = default);
    Task<KitchenOrderResponse> DecideAsync(int restaurantId, int orderId, DecideOrderCancellationRequest request, CancellationToken token = default);
}
