using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record CreateWaiterOrderCommand(int RestaurantId, int TableSessionId, Guid RequestId,
    CreateWaiterOrderRequest Request) : IRequest<CreateOrderResult>;

public sealed class CreateWaiterOrderCommandHandler(IOrderService service)
    : IRequestHandler<CreateWaiterOrderCommand, CreateOrderResult>
{
    public Task<CreateOrderResult> Handle(CreateWaiterOrderCommand request, CancellationToken cancellationToken)
        => service.CreateForWaiterAsync(request.RestaurantId, request.TableSessionId,
            request.RequestId, request.Request, cancellationToken);
}
