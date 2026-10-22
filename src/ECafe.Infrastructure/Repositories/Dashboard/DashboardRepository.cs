using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository(ECafeDbContext db) : IDashboardRepository
{
    /// <summary>Bütün oxuma sorğularına eyni restoran sərhədini subquery kimi tətbiq edir; seçilən ID hüquqları genişləndirmir.</summary>
    private IQueryable<Domain.Entities.Restaurant> Restaurants(DashboardScope scope)
        => db.Restaurants.AsNoTracking().Where(restaurant =>
            (!scope.RestaurantId.HasValue || restaurant.Id == scope.RestaurantId) &&
            (scope.Role == RoleCode.SuperAdmin || restaurant.IsActive && db.UserRestaurants.Any(assignment =>
                assignment.RestaurantId == restaurant.Id && assignment.UserId == scope.UserId && assignment.IsActive &&
                assignment.RoleId == (int)scope.Role && assignment.User.IsActive)));

    /// <summary>Stok və xidmət rəqəmləri üçün istifadə edilən, bağlanmamış masa sessiyalarını məhdudlaşdırır.</summary>
    private IQueryable<Domain.Entities.TableSession> OpenSessions(DashboardScope scope)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var open = StatusIds.TableSession(TableSessionStatus.Open);
        return db.TableSessions.AsNoTracking().Where(session => ids.Contains(session.RestaurantId) &&
            session.StatusId == open && session.ClosedAt == null && session.Table.RestaurantId == session.RestaurantId);
    }
}
