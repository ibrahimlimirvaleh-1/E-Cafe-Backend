namespace ECafe.Application.Services.Restaurant.Abstract;

public interface IRestaurantDepositService
{
    Task<decimal> ResolveAmountAsync(ECafe.Domain.Entities.Restaurant restaurant, DateTimeOffset reservedAt, CancellationToken cancellationToken = default);
}
