using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record SendOrderToKitchenCommand(int RestaurantId, int TableSessionId, int OrderId)
    : IRequest<SessionOrderResponse>;

public sealed class SendOrderToKitchenCommandHandler(IKitchenOrderService service)
    : IRequestHandler<SendOrderToKitchenCommand, SessionOrderResponse>
{
    /// <summary>Ofisiantın göndəriş əmrini eyni sifariş üzrə təkrara davamlı mətbəx xidmətinə ötürür.</summary>
    public Task<SessionOrderResponse> Handle(SendOrderToKitchenCommand request, CancellationToken cancellationToken)
        => service.SendAsync(request.RestaurantId, request.TableSessionId, request.OrderId, cancellationToken);
}
