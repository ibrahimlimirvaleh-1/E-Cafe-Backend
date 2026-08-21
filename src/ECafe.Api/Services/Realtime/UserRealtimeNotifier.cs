using ECafe.Api.Realtime;
using ECafe.Application.Services.Realtime.Abstract;
using Microsoft.AspNetCore.SignalR;

namespace ECafe.Api.Services.Realtime;

public sealed class UserRealtimeNotifier : IUserRealtimeNotifier
{
    private const string UserDeactivatedEvent = "UserDeactivated";
    private const string UserRoleChangedEvent = "UserRoleChanged";
    private const string RestaurantAccessChangedEvent = "RestaurantAccessChanged";

    private readonly IHubContext<UserEventsHub> _hubContext;

    public UserRealtimeNotifier(IHubContext<UserEventsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyUserDeactivatedAsync(int userId, string message, CancellationToken cancellationToken = default)
        => SendUserEventAsync(userId, UserDeactivatedEvent, new
        {
            message,
            occurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    public Task NotifyUserRoleChangedAsync(int userId, string message, CancellationToken cancellationToken = default)
        => SendUserEventAsync(userId, UserRoleChangedEvent, new
        {
            message,
            occurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    public Task NotifyRestaurantAccessChangedAsync(
        int userId,
        int restaurantId,
        string reason,
        string message,
        CancellationToken cancellationToken = default)
        => SendUserEventAsync(userId, RestaurantAccessChangedEvent, new
        {
            restaurantId,
            reason,
            message,
            occurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    private Task SendUserEventAsync(
        int userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients.User(userId.ToString())
            .SendAsync(eventName, payload, cancellationToken);
    }
}
