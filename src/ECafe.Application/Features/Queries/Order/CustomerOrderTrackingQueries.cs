using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Order;

public sealed record GetCustomerTableSessionsQuery(PaginationFilter Pagination)
    : IRequest<PaginatedList<CustomerTableSessionResponse>>;
public sealed record GetCustomerTableSessionQuery(int TableSessionId) : IRequest<CustomerTableSessionResponse>;
public sealed record GetCustomerSessionOrdersQuery(int TableSessionId, PaginationFilter Pagination)
    : IRequest<PaginatedList<CustomerSessionOrderResponse>>;
public sealed record GetCustomerSessionOrderQuery(int TableSessionId, int OrderId) : IRequest<CustomerSessionOrderResponse>;

public sealed class CustomerOrderTrackingQueryHandler(ICustomerOrderTrackingService service)
    : IRequestHandler<GetCustomerTableSessionsQuery, PaginatedList<CustomerTableSessionResponse>>,
        IRequestHandler<GetCustomerTableSessionQuery, CustomerTableSessionResponse>,
        IRequestHandler<GetCustomerSessionOrdersQuery, PaginatedList<CustomerSessionOrderResponse>>,
        IRequestHandler<GetCustomerSessionOrderQuery, CustomerSessionOrderResponse>
{
    public Task<PaginatedList<CustomerTableSessionResponse>> Handle(GetCustomerTableSessionsQuery request, CancellationToken cancellationToken)
        => service.GetMySessionsAsync(request.Pagination, cancellationToken);
    public Task<CustomerTableSessionResponse> Handle(GetCustomerTableSessionQuery request, CancellationToken cancellationToken)
        => service.GetSessionAsync(request.TableSessionId, cancellationToken);
    public Task<PaginatedList<CustomerSessionOrderResponse>> Handle(GetCustomerSessionOrdersQuery request, CancellationToken cancellationToken)
        => service.GetOrdersAsync(request.TableSessionId, request.Pagination, cancellationToken);
    public Task<CustomerSessionOrderResponse> Handle(GetCustomerSessionOrderQuery request, CancellationToken cancellationToken)
        => service.GetOrderAsync(request.TableSessionId, request.OrderId, cancellationToken);
}
