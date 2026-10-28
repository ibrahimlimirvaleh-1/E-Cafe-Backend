using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Order;

public sealed record RequestWaiterCancellationCommand(int RestaurantId, int SessionId, int OrderId, CancelOrderRequest Body) : IRequest<SessionOrderResponse>;
public sealed record RequestCustomerCancellationCommand(int SessionId, int OrderId, CancelOrderRequest Body) : IRequest<CustomerSessionOrderResponse>;
public sealed record ForwardCancellationCommand(int RestaurantId, int SessionId, int OrderId) : IRequest<SessionOrderResponse>;
public sealed record KitchenCancellationQuery(int RestaurantId, int OrderId) : IRequest<KitchenOrderResponse>;
public sealed record KitchenStockReturnPreviewQuery(int RestaurantId, int OrderId) : IRequest<KitchenStockReturnPreviewResponse>;
public sealed record DecideCancellationCommand(int RestaurantId, int OrderId, DecideOrderCancellationRequest Body) : IRequest<KitchenOrderResponse>;

public sealed class OrderCancellationRequestHandler(IOrderCancellationRequestService service) :
    IRequestHandler<RequestWaiterCancellationCommand, SessionOrderResponse>,
    IRequestHandler<RequestCustomerCancellationCommand, CustomerSessionOrderResponse>,
    IRequestHandler<ForwardCancellationCommand, SessionOrderResponse>,
    IRequestHandler<KitchenCancellationQuery, KitchenOrderResponse>,
    IRequestHandler<KitchenStockReturnPreviewQuery, KitchenStockReturnPreviewResponse>,
    IRequestHandler<DecideCancellationCommand, KitchenOrderResponse>
{
    /// <summary>Ofisiantın sorğusunu rol və sessiya qorumalı xidmətə ötürür.</summary>
    public Task<SessionOrderResponse> Handle(RequestWaiterCancellationCommand request, CancellationToken token)
        => service.RequestForWaiterAsync(request.RestaurantId, request.SessionId, request.OrderId, request.Body, token);
    /// <summary>Müştəri identifikatorunu body-dən qəbul etmədən şəxsi sorğunu yaradır.</summary>
    public Task<CustomerSessionOrderResponse> Handle(RequestCustomerCancellationCommand request, CancellationToken token)
        => service.RequestForCustomerAsync(request.SessionId, request.OrderId, request.Body, token);
    /// <summary>Ofisiantın ilk ötürməsini eyni sifariş ID-si ilə təkrara davamlı icra edir.</summary>
    public Task<SessionOrderResponse> Handle(ForwardCancellationCommand request, CancellationToken token)
        => service.ForwardAsync(request.RestaurantId, request.SessionId, request.OrderId, token);
    /// <summary>Mətbəxə qeyri-müəyyən qərar nəticəsini öz restoranında yoxlamağa imkan verir.</summary>
    public Task<KitchenOrderResponse> Handle(KitchenCancellationQuery request, CancellationToken token)
        => service.GetForKitchenAsync(request.RestaurantId, request.OrderId, token);
    /// <summary>Ərzaq seçimini yalnız canlı rol və ilkin sərfiyyat qorumalı mətbəx xidmətindən oxuyur.</summary>
    public Task<KitchenStockReturnPreviewResponse> Handle(KitchenStockReturnPreviewQuery request, CancellationToken token)
        => service.GetStockReturnPreviewAsync(request.RestaurantId, request.OrderId, token);
    /// <summary>Qərarı canlı mətbəx rolu və ortaq masa kilidində icra edən xidmətə ötürür.</summary>
    public Task<KitchenOrderResponse> Handle(DecideCancellationCommand request, CancellationToken token)
        => service.DecideAsync(request.RestaurantId, request.OrderId, request.Body, token);
}
