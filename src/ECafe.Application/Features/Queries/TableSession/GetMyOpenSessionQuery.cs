using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.TableSession;

public sealed record GetMyOpenSessionQuery(int RestaurantId, int TableSessionId)
    : IRequest<WaiterTableSessionResponse>;

public sealed class GetMyOpenSessionQueryHandler(ITableSessionService service)
    : IRequestHandler<GetMyOpenSessionQuery, WaiterTableSessionResponse>
{
    public Task<WaiterTableSessionResponse> Handle(GetMyOpenSessionQuery request, CancellationToken cancellationToken)
        => service.GetMyOpenSessionAsync(request.RestaurantId, request.TableSessionId, cancellationToken);
}
