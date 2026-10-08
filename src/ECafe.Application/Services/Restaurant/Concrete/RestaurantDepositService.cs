using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Validation;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Restaurant.Concrete;

public sealed class RestaurantDepositService : BaseManager, IRestaurantDepositService
{
    private readonly IBaseRepository<RestaurantDepositRule> _rules;
    private readonly IRestaurantRepository _restaurants;
    private readonly IAuditLogService _auditLogs;

    public RestaurantDepositService(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        IBaseRepository<RestaurantDepositRule> rules,
        IRestaurantRepository restaurants,
        IAuditLogService auditLogs)
        : base(httpContextAccessor, mapper, configuration)
    {
        _rules = rules;
        _restaurants = restaurants;
        _auditLogs = auditLogs;
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

    public async Task<IReadOnlyDictionary<int, decimal>> GetAmountsForDateAsync(
        IReadOnlyCollection<int> restaurantIds,
        DateOnly reservationDate)
    {
        if (restaurantIds.Count == 0)
            return new Dictionary<int, decimal>();

        var ids = restaurantIds.ToArray();
        return await _rules.Query(rule => ids.Contains(rule.RestaurantId) && rule.ReservationDate == reservationDate)
            .ToDictionaryAsync(rule => rule.RestaurantId, rule => rule.Amount);
    }

    public Task SetRuleAsync(int restaurantId, DateOnly reservationDate, decimal amount)
        => ChangeRuleAsync(restaurantId, reservationDate, amount);

    public Task RemoveRuleAsync(int restaurantId, DateOnly reservationDate)
        => ChangeRuleAsync(restaurantId, reservationDate, null);

    // Günlük depozit qaydasını dəyişməzdən əvvəl restoran, tarix və məbləği yoxlayır.
    private async Task ChangeRuleAsync(int restaurantId, DateOnly reservationDate, decimal? amount)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

        EnsureCurrentUserCanAccessRestaurant(restaurantId);

        var restaurant = await _restaurants.GetByIdAsync(restaurantId);
        if (restaurant is null || !restaurant.IsActive)
            throw new BusinessRuleException(ErrorCode.RestaurantNotFound);

        var localNow = RestaurantTimeZoneConverter.ToRestaurantLocalTime(DateTimeOffset.UtcNow, restaurant.TimeZone);
        if (reservationDate < DateOnly.FromDateTime(localNow.DateTime))
            throw new BusinessRuleException(ErrorCode.PastDepositRuleCannotBeChanged);
        if (amount.HasValue && !RestaurantDepositAmount.IsValid(amount.Value))
            throw new BusinessRuleException(RestaurantDepositAmount.InvalidAmountMessage);

        var rule = await _rules.QueryTracked(x =>
                x.RestaurantId == restaurantId && x.ReservationDate == reservationDate)
            .FirstOrDefaultAsync();

        if ((!amount.HasValue && rule is null) || (amount.HasValue && rule?.Amount == amount.Value))
            return;

        if (!amount.HasValue)
        {
            await _rules.Delete(rule!);
        }
        else if (rule is null)
        {
            await _rules.Add(new RestaurantDepositRule
            {
                RestaurantId = restaurantId,
                ReservationDate = reservationDate,
                Amount = amount.Value
            });
        }
        else
        {
            rule.Amount = amount.Value;
        }

        await _rules.SaveChangesAsync();
        await _auditLogs.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.RestaurantUpdated,
            new { DepositDate = reservationDate, Amount = amount },
            AuditEntityTypes.Restaurant,
            restaurantId,
            restaurant.Name);
    }
}
