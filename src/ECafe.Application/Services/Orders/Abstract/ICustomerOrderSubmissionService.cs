using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Services.Orders.Abstract;

public interface ICustomerOrderSubmissionService
{
    Task<CreateCustomerOrderResult> CreateAsync(int tableSessionId, Guid requestId,
        CreateCustomerOrderRequest request, CancellationToken cancellationToken = default);
}
