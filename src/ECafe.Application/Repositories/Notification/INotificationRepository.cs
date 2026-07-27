using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Notification
{
    public interface INotificationRepository : IBaseRepository<Domain.Entities.Notification>
    {
        Task<List<Domain.Entities.Notification>> GetByUserAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<List<Domain.Entities.Notification>> GetUnreadByUserAsync(int userId);
        Task<Domain.Entities.Notification?> GetByUserAndIdTrackedAsync(int userId, int notificationId);
    }
}
