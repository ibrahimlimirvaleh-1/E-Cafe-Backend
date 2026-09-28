namespace ECafe.Application.Services.Realtime.Abstract;

public interface INotificationChangeDispatcher
{
    Task NotifyChangedAsync(int userId, CancellationToken cancellationToken = default);
    Task FlushAsync(CancellationToken cancellationToken = default);
    void Discard();
}
