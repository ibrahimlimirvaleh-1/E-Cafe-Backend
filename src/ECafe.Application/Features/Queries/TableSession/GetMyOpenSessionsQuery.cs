using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.TableSession;

public sealed record GetMyOpenSessionsQuery(int RestaurantId, PaginationFilter Pagination)
    : IRequest<PaginatedList<WaiterTableSessionResponse>>;

public sealed class GetMyOpenSessionsQueryHandler(ITableSessionService service)
    : IRequestHandler<GetMyOpenSessionsQuery, PaginatedList<WaiterTableSessionResponse>>
{
    public Task<PaginatedList<WaiterTableSessionResponse>> Handle(
        GetMyOpenSessionsQuery request, CancellationToken cancellationToken)
        => service.GetMyOpenSessionsAsync(request.RestaurantId, request.Pagination, cancellationToken);
}
