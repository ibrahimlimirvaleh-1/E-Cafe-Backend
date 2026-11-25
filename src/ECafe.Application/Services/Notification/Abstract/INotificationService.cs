using ECafe.Application.DTOs.Notification;

using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Notification.Abstract
{
    public interface INotificationService
    {
        Task CreateAsync(CreateNotificationRequest request);
        Task<List<NotificationResponse>> GetMyNotificationsAsync();
        Task<PaginatedList<NotificationResponse>> GetMyNotificationsPageAsync(PaginationFilter pagination,
            CancellationToken cancellationToken = default);
        Task<UnreadNotificationCountResponse> GetUnreadCountAsync();
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync();
    }
}
