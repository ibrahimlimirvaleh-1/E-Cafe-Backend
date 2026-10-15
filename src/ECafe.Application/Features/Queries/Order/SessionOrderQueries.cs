using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Order;

public sealed record GetSessionOrdersQuery(int RestaurantId, int TableSessionId, PaginationFilter Pagination)
    : IRequest<PaginatedList<SessionOrderResponse>>;
public sealed record GetSessionOrderQuery(int RestaurantId, int TableSessionId, int OrderId)
    : IRequest<SessionOrderResponse>;

public sealed class GetSessionOrdersQueryHandler(IOrderService service)
    : IRequestHandler<GetSessionOrdersQuery, PaginatedList<SessionOrderResponse>>
{
    public Task<PaginatedList<SessionOrderResponse>> Handle(GetSessionOrdersQuery request, CancellationToken cancellationToken)
        => service.GetForSessionAsync(request.RestaurantId, request.TableSessionId, request.Pagination, cancellationToken);
}

public sealed class GetSessionOrderQueryHandler(IOrderService service) : IRequestHandler<GetSessionOrderQuery, SessionOrderResponse>
{
    public Task<SessionOrderResponse> Handle(GetSessionOrderQuery request, CancellationToken cancellationToken)
        => service.GetByIdAsync(request.RestaurantId, request.TableSessionId, request.OrderId, cancellationToken);
}
