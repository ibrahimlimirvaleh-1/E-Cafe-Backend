using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.TableSession;

public sealed record ContinueTableServiceCommand(int RestaurantId, int TableSessionId, ContinueTableServiceRequest Body) : IRequest<WaiterTableSessionResponse>;
public sealed class ContinueTableServiceCommandHandler(ITableSessionCloseReviewService service) : IRequestHandler<ContinueTableServiceCommand, WaiterTableSessionResponse>
{
    public Task<WaiterTableSessionResponse> Handle(ContinueTableServiceCommand request, CancellationToken cancellationToken)
        => service.ContinueServiceAsync(request.RestaurantId, request.TableSessionId, request.Body, cancellationToken);
}
