using ECafe.Application.Repositories.Notification;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
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
            => VisibleToUser(userId, restaurantId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        public Task<List<Domain.Entities.Notification>> GetUnreadByUserAsync(int userId, int? restaurantId)
            => VisibleToUser(userId, restaurantId, tracked: true)
                .Where(x => x.StatusId == (int)NotificationStatus.Unread)
                .ToListAsync();

        public Task<int> GetUnreadCountAsync(int userId, int? restaurantId)
            => VisibleToUser(userId, restaurantId)
                .Where(x => x.StatusId == (int)NotificationStatus.Unread)
                .CountAsync();

        public Task<Domain.Entities.Notification?> GetByUserAndIdTrackedAsync(int userId, int notificationId, int? restaurantId)
            => VisibleToUser(userId, restaurantId, tracked: true)
                .Where(x => x.Id == notificationId)
                .FirstOrDefaultAsync();

        /// <summary>Bildiriş oxunuşunda aidiyyəti eyni SQL sorğusunda yenidən yoxlayır; boş scope işçiyə bütün restoranları açmır.</summary>
        private IQueryable<Domain.Entities.Notification> VisibleToUser(int userId, int? restaurantId, bool tracked = false)
        {
            var query = tracked ? QueryTracked() : Query();
            return query.Where(notification => notification.UserId == userId &&
                (!restaurantId.HasValue || !notification.RestaurantId.HasValue || notification.RestaurantId == restaurantId) &&
                Context.Users.Any(user => user.Id == userId && user.IsActive &&
                    (user.RoleId == (int)RoleCode.Customer || user.RoleId == (int)RoleCode.SuperAdmin ||
                     restaurantId.HasValue && Context.UserRestaurants.Any(assignment => assignment.UserId == userId &&
                         assignment.RestaurantId == restaurantId && assignment.IsActive && assignment.Restaurant.IsActive &&
                         UserProfileAccessPolicy.RestaurantRoleIds.Contains(assignment.RoleId)))));
        }
    }
}
