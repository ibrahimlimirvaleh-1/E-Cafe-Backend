using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Order;

public sealed record GetKitchenOrdersQuery(int RestaurantId, PaginationFilter Pagination)
    : IRequest<PaginatedList<KitchenOrderResponse>>;

public sealed class GetKitchenOrdersQueryHandler(IKitchenOrderService service)
    : IRequestHandler<GetKitchenOrdersQuery, PaginatedList<KitchenOrderResponse>>
{
    /// <summary>Restoranın mətbəx növbəsini səlahiyyət və səhifələmə yoxlamalarından keçirərək oxuyur.</summary>
    public Task<PaginatedList<KitchenOrderResponse>> Handle(GetKitchenOrdersQuery request, CancellationToken cancellationToken)
        => service.GetQueueAsync(request.RestaurantId, request.Pagination, cancellationToken);
}
