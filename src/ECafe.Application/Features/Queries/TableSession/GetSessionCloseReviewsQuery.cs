using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.TableSession;

public sealed record GetSessionCloseReviewsQuery(int RestaurantId, PaginationFilter Pagination) : IRequest<PaginatedList<WaiterTableSessionResponse>>;
public sealed class GetSessionCloseReviewsQueryHandler(ITableSessionCloseReviewService service) : IRequestHandler<GetSessionCloseReviewsQuery, PaginatedList<WaiterTableSessionResponse>>
{
    public Task<PaginatedList<WaiterTableSessionResponse>> Handle(GetSessionCloseReviewsQuery request, CancellationToken cancellationToken)
        => service.GetReviewsAsync(request.RestaurantId, request.Pagination, cancellationToken);
}
