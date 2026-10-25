using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IOrderCancellationService
{
    Task<SessionOrderResponse> CancelForWaiterAsync(int restaurantId, int tableSessionId, int orderId,
        CancelOrderRequest request, CancellationToken cancellationToken = default);
    Task<CustomerSessionOrderResponse> CancelForCustomerAsync(int tableSessionId, int orderId,
        CancelOrderRequest request, CancellationToken cancellationToken = default);
}
