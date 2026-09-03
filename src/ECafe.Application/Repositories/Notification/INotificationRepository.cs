using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Notification
{
    public interface INotificationRepository : IBaseRepository<Domain.Entities.Notification>
    {
        Task<List<Domain.Entities.Notification>> GetByUserAsync(int userId, int? restaurantId);
        Task<int> GetUnreadCountAsync(int userId, int? restaurantId);
        Task<List<Domain.Entities.Notification>> GetUnreadByUserAsync(int userId, int? restaurantId);
        Task<Domain.Entities.Notification?> GetByUserAndIdTrackedAsync(int userId, int notificationId, int? restaurantId);
    }
}