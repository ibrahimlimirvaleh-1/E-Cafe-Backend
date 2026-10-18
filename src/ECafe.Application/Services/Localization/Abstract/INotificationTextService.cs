using ECafe.Application.DTOs.Notification;
using ECafe.Domain.Entities;

namespace ECafe.Application.Services.Localization.Abstract;

public interface INotificationTextService
{
    Task PrepareAsync(ECafe.Domain.Entities.Notification notification, CancellationToken cancellationToken);
    void LocalizeForReader(ECafe.Domain.Entities.Notification notification, NotificationResponse response);
}
