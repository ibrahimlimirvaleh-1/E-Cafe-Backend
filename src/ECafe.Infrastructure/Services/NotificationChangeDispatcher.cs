using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Infrastructure.Context;
using Microsoft.Extensions.Logging;

namespace ECafe.Infrastructure.Services;

internal sealed class NotificationChangeDispatcher : INotificationChangeDispatcher
{
    private readonly ECafeDbContext _context;
    private readonly IUserRealtimeNotifier _notifier;
    private readonly ILogger<NotificationChangeDispatcher> _logger;
    private readonly HashSet<int> _pendingUserIds = [];

    public NotificationChangeDispatcher(
        ECafeDbContext context,
        IUserRealtimeNotifier notifier,
        ILogger<NotificationChangeDispatcher> logger)
    {
        _context = context;
        _notifier = notifier;
        _logger = logger;
    }

    public Task NotifyChangedAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            _pendingUserIds.Add(userId);
            return Task.CompletedTask;
        }

        return PublishAsync(userId, cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var userIds = _pendingUserIds.ToArray();
        _pendingUserIds.Clear();

        foreach (var userId in userIds)
            await PublishAsync(userId, cancellationToken);
    }

    public void Discard() => _pendingUserIds.Clear();

    // Dəyişən bildiriş siyahısını istifadəçinin real vaxt kanalına çatdırır.
    private async Task PublishAsync(int userId, CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.NotifyNotificationsChangedAsync(userId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Realtime notification update failed for user {UserId}.", userId);
        }
    }
}
