using ECafe.Application.DTOs.Notification;

namespace ECafe.Application.Services.Notification.Abstract
{
    public interface INotificationService
    {
        Task CreateAsync(CreateNotificationRequest request);
        Task<List<NotificationResponse>> GetMyNotificationsAsync();
        Task<UnreadNotificationCountResponse> GetUnreadCountAsync();
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync();
    }
}
