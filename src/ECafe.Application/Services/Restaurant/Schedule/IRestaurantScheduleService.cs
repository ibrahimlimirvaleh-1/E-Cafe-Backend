using ECafe.Application.DTOs.Restaurant;
using ECafe.Domain.Entities;

namespace ECafe.Application.Services.Restaurant.Schedule;

public interface IRestaurantScheduleService
{
    Task<ScheduleChangeResponse?> GetAsync(int restaurantId, CancellationToken cancellationToken);
    Task<ScheduleChangeResponse> ProposeAsync(int restaurantId, ProposeScheduleRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeResponse> ApplyAsync(int restaurantId, ScheduleActionRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeResponse> WithdrawAsync(int restaurantId, ScheduleActionRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeResponse> AcknowledgeSessionAsync(int restaurantId, int consentId, ScheduleDecisionRequest request, CancellationToken cancellationToken);
    Task<CustomerScheduleOfferResponse?> GetCustomerOfferAsync(int reservationId, CancellationToken cancellationToken);
    Task<CustomerScheduleOfferResponse> RespondAsync(int reservationId, ScheduleDecisionRequest request, CancellationToken cancellationToken);
    Task EnsureDirectChangeAllowedAsync(Domain.Entities.Restaurant restaurant, List<RestaurantWorkingHour> hours,
        string timeZone, CancellationToken cancellationToken);
}
