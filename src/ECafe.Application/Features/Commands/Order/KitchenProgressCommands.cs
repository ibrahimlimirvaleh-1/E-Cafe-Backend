using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record AcceptKitchenOrderCommand(int RestaurantId, int OrderId) : IRequest<KitchenOrderResponse>;
public sealed record StartKitchenOrderCommand(int RestaurantId, int OrderId) : IRequest<KitchenOrderResponse>;
public sealed record ReadyKitchenOrderCommand(int RestaurantId, int OrderId) : IRequest<KitchenOrderResponse>;

public sealed class KitchenProgressCommandHandler(IKitchenOrderService service)
    : IRequestHandler<AcceptKitchenOrderCommand, KitchenOrderResponse>,
      IRequestHandler<StartKitchenOrderCommand, KitchenOrderResponse>,
      IRequestHandler<ReadyKitchenOrderCommand, KitchenOrderResponse>
{
    /// <summary>Qəbul əmrini mətbəx xidmətinin rol və status yoxlamalarına ötürür.</summary>
    public Task<KitchenOrderResponse> Handle(AcceptKitchenOrderCommand request, CancellationToken cancellationToken)
        => service.AcceptAsync(request.RestaurantId, request.OrderId, cancellationToken);

    /// <summary>Hazırlama əmrini mətbəx xidmətinə ötürür; target status müştəri body-sindən götürülmür.</summary>
    public Task<KitchenOrderResponse> Handle(StartKitchenOrderCommand request, CancellationToken cancellationToken)
        => service.StartPreparingAsync(request.RestaurantId, request.OrderId, cancellationToken);

    /// <summary>Hazır əmrini status, audit və məsul ofisiant bildirişi ilə birlikdə icra etdirir.</summary>
    public Task<KitchenOrderResponse> Handle(ReadyKitchenOrderCommand request, CancellationToken cancellationToken)
        => service.MarkReadyAsync(request.RestaurantId, request.OrderId, cancellationToken);
}
