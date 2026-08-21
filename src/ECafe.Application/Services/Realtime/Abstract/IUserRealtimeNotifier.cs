namespace ECafe.Application.Services.Realtime.Abstract;

public interface IUserRealtimeNotifier
{
    Task NotifyUserDeactivatedAsync(int userId, string message, CancellationToken cancellationToken = default);
}
