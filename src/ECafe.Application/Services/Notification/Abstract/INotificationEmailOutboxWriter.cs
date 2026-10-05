namespace ECafe.Application.Services.Notification.Abstract;

public interface INotificationEmailOutboxWriter
{
    Task EnqueueAsync(ECafe.Domain.Entities.Notification notification);
}
