using ECafe.Domain.Entities;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IOrderStockReturnService
{
    Task RestoreAsync(Order order, CancellationToken cancellationToken = default);
}
