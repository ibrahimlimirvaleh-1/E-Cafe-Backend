namespace ECafe.Application.Services.Realtime.Abstract;

public interface IUserRealtimeNotifier
{
    Task NotifyOrderChangedAsync(IReadOnlyCollection<int> userIds, OrderChangedEvent change,
        CancellationToken cancellationToken = default);

    Task NotifyNotificationsChangedAsync(int userId, CancellationToken cancellationToken = default);

    Task NotifyUserDeactivatedAsync(int userId, string message, CancellationToken cancellationToken = default);

    Task NotifyUserRoleChangedAsync(int userId, string message, CancellationToken cancellationToken = default);

    Task NotifyRestaurantAccessChangedAsync(
        int userId,
        int restaurantId,
        string reason,
        string message,
        CancellationToken cancellationToken = default);
}
