using ECafe.Domain.Entities;

namespace ECafe.Application.Repositories.Restaurant;

public interface IRestaurantScheduleRepository
{
    Task AcquireLocksAsync(int restaurantId, CancellationToken cancellationToken);
    Task<Domain.Entities.Restaurant?> GetRestaurantAsync(int restaurantId, CancellationToken cancellationToken);
    Task<RestaurantScheduleChange?> GetPendingAsync(int restaurantId, CancellationToken cancellationToken);
    Task<RestaurantScheduleChange?> GetLatestAsync(int restaurantId, CancellationToken cancellationToken);
    Task<List<Domain.Entities.Reservation>> GetActiveReservationsAsync(int restaurantId, DateTime now, CancellationToken cancellationToken);
    Task<List<Domain.Entities.TableSession>> GetOpenSessionsAsync(int restaurantId, CancellationToken cancellationToken);
    Task<RestaurantScheduleConsent?> GetCustomerConsentAsync(int reservationId, int userId, CancellationToken cancellationToken);
    Task AddAsync(RestaurantScheduleChange change, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
