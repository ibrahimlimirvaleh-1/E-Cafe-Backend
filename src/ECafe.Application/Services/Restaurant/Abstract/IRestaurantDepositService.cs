namespace ECafe.Application.Services.Restaurant.Abstract;

public interface IRestaurantDepositService
{
    Task<decimal> ResolveAmountAsync(ECafe.Domain.Entities.Restaurant restaurant, DateTimeOffset reservedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, decimal>> GetAmountsForDateAsync(IReadOnlyCollection<int> restaurantIds, DateOnly reservationDate);

    Task SetRuleAsync(int restaurantId, DateOnly reservationDate, decimal amount);

    Task RemoveRuleAsync(int restaurantId, DateOnly reservationDate);
}
