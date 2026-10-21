using ECafe.Domain.Entities;

namespace ECafe.Application.Services.Orders.Abstract;

public interface IOrderStockService
{
    Task ConsumeAsync(Order order, CancellationToken cancellationToken);
}
