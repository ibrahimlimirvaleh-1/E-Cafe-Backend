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

        public Task<List<Domain.Entities.Notification>> GetByUserAsync(int userId)
            => Query(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        public Task<List<Domain.Entities.Notification>> GetUnreadByUserAsync(int userId)
            => QueryTracked(x => x.UserId == userId && x.StatusId == (int)NotificationStatus.Unread)
                .ToListAsync();

        public Task<int> GetUnreadCountAsync(int userId)
            => Query(x => x.UserId == userId && x.StatusId == (int)NotificationStatus.Unread)
                .CountAsync();

        public Task<Domain.Entities.Notification?> GetByUserAndIdTrackedAsync(int userId, int notificationId)
            => QueryTracked(x => x.UserId == userId && x.Id == notificationId)
                .FirstOrDefaultAsync();
    }
}
