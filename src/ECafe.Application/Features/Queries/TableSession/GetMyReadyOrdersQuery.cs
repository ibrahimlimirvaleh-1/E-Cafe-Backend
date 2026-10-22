using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.TableSession;

public sealed record GetMyReadyOrdersQuery(int RestaurantId, PaginationFilter Pagination)
    : IRequest<PaginatedList<WaiterReadyOrderResponse>>;

public sealed class GetMyReadyOrdersQueryHandler(ITableSessionService service)
    : IRequestHandler<GetMyReadyOrdersQuery, PaginatedList<WaiterReadyOrderResponse>>
{
    public Task<PaginatedList<WaiterReadyOrderResponse>> Handle(
        GetMyReadyOrdersQuery request, CancellationToken cancellationToken)
        => service.GetMyReadyOrdersAsync(request.RestaurantId, request.Pagination, cancellationToken);
}
