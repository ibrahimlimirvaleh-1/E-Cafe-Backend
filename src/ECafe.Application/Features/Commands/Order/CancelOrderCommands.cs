using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record CancelWaiterOrderCommand(int RestaurantId, int TableSessionId, int OrderId, CancelOrderRequest Body)
    : IRequest<SessionOrderResponse>;
public sealed record CancelCustomerOrderCommand(int TableSessionId, int OrderId, CancelOrderRequest Body)
    : IRequest<CustomerSessionOrderResponse>;

public sealed class CancelOrderCommandHandler(IOrderCancellationService service)
    : IRequestHandler<CancelWaiterOrderCommand, SessionOrderResponse>,
      IRequestHandler<CancelCustomerOrderCommand, CustomerSessionOrderResponse>
{
    /// <summary>Ofisiantın ləğvini canlı rol və sessiya yoxlamalarını edən xidmətə ötürür.</summary>
    public Task<SessionOrderResponse> Handle(CancelWaiterOrderCommand request, CancellationToken cancellationToken)
        => service.CancelForWaiterAsync(request.RestaurantId, request.TableSessionId, request.OrderId, request.Body, cancellationToken);

    /// <summary>Müştərinin ləğvində restoran və istifadəçi ID-sini brauzerdən qəbul etmir.</summary>
    public Task<CustomerSessionOrderResponse> Handle(CancelCustomerOrderCommand request, CancellationToken cancellationToken)
        => service.CancelForCustomerAsync(request.TableSessionId, request.OrderId, request.Body, cancellationToken);
}
