using ECafe.Application.Repository;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Restaurant.Concrete;

public sealed class RestaurantDepositService : IRestaurantDepositService
{
    private readonly IBaseRepository<RestaurantDepositRule> _rules;

    public RestaurantDepositService(IBaseRepository<RestaurantDepositRule> rules)
    {
        _rules = rules;
    }

    public async Task<decimal> ResolveAmountAsync(ECafe.Domain.Entities.Restaurant restaurant, DateTimeOffset reservedAt, CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(
            RestaurantTimeZoneConverter.ToRestaurantLocalTime(reservedAt, restaurant.TimeZone).DateTime);
        return await _rules.Query(rule =>
                rule.RestaurantId == restaurant.Id && rule.ReservationDate == localDate)
            .Select(rule => (decimal?)rule.Amount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }
}
