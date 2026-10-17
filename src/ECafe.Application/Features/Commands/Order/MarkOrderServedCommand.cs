using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record MarkOrderServedCommand(int RestaurantId, int TableSessionId, int OrderId)
    : IRequest<SessionOrderResponse>;

public sealed class MarkOrderServedCommandHandler(IOrderService service)
    : IRequestHandler<MarkOrderServedCommand, SessionOrderResponse>
{
    /// <summary>Ofisiantın təqdim əməliyyatını serverdə hazır sifariş və sessiya icazəsini yoxlayan xidmətə ötürür.</summary>
    public Task<SessionOrderResponse> Handle(MarkOrderServedCommand request, CancellationToken cancellationToken)
        => service.MarkServedAsync(request.RestaurantId, request.TableSessionId, request.OrderId, cancellationToken);
}
