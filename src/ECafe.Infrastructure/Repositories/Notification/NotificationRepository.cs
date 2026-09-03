using ECafe.Application.Repositories.Notification;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Notification
{
    public class NotificationRepository : BaseRepository<Domain.Entities.Notification>, INotificationRepository
    {
        public NotificationRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<List<Domain.Entities.Notification>> GetByUserAsync(int userId, int? restaurantId)
            => Query(x => x.UserId == userId && (!restaurantId.HasValue || !x.RestaurantId.HasValue || x.RestaurantId == restaurantId.Value))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        public Task<List<Domain.Entities.Notification>> GetUnreadByUserAsync(int userId, int? restaurantId)
            => QueryTracked(x =>
                    x.UserId == userId &&
                    x.StatusId == (int)NotificationStatus.Unread &&
                    (!restaurantId.HasValue || !x.RestaurantId.HasValue || x.RestaurantId == restaurantId.Value))
                .ToListAsync();

        public Task<int> GetUnreadCountAsync(int userId, int? restaurantId)
            => Query(x =>
                    x.UserId == userId &&
                    x.StatusId == (int)NotificationStatus.Unread &&
                    (!restaurantId.HasValue || !x.RestaurantId.HasValue || x.RestaurantId == restaurantId.Value))
                .CountAsync();

        public Task<Domain.Entities.Notification?> GetByUserAndIdTrackedAsync(int userId, int notificationId, int? restaurantId)
            => QueryTracked(x =>
                    x.UserId == userId &&
                    x.Id == notificationId &&
                    (!restaurantId.HasValue || !x.RestaurantId.HasValue || x.RestaurantId == restaurantId.Value))
                .FirstOrDefaultAsync();
    }
}
