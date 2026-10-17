using ECafe.Application.DTOs.Order;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Orders.Abstract;

public interface ICustomerOrderTrackingService
{
    Task<PaginatedList<CustomerTableSessionResponse>> GetMySessionsAsync(PaginationFilter pagination,
        CancellationToken cancellationToken = default);
    Task<CustomerTableSessionResponse> GetSessionAsync(int tableSessionId, CancellationToken cancellationToken = default);
    Task<PaginatedList<CustomerSessionOrderResponse>> GetOrdersAsync(int tableSessionId, PaginationFilter pagination,
        CancellationToken cancellationToken = default);
    Task<CustomerSessionOrderResponse> GetOrderAsync(int tableSessionId, int orderId,
        CancellationToken cancellationToken = default);
}
