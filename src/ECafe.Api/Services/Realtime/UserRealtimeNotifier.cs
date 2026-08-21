using ECafe.Api.Realtime;
using ECafe.Application.Services.Realtime.Abstract;
using Microsoft.AspNetCore.SignalR;

namespace ECafe.Api.Services.Realtime;

public sealed class UserRealtimeNotifier : IUserRealtimeNotifier
{
    private readonly IHubContext<UserEventsHub> _hubContext;

    public UserRealtimeNotifier(IHubContext<UserEventsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyUserDeactivatedAsync(int userId, string message, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            message,
            occurredAtUtc = DateTime.UtcNow
        };

        return _hubContext.Clients.User(userId.ToString())
            .SendAsync("UserDeactivated", payload, cancellationToken);
    }
}
