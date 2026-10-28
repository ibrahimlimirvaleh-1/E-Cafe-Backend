using ECafe.Domain.Entities;
using ECafe.Domain.ValueObjects;
using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IOrderStockReturnService
{
    Task RestoreAsync(Order order, CancellationToken cancellationToken = default);
    Task<bool> RestorePartialAsync(Order order, IReadOnlyList<OrderStockReturnQuantity> quantities, CancellationToken cancellationToken = default);
    Task<KitchenStockReturnPreviewResponse> PreviewAsync(Order order, CancellationToken cancellationToken = default);
}
