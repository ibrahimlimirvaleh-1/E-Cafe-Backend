using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Aktiv istifadəçini və canlı restoran rolunu yoxlayır; JWT-də köhnə qalmış təyinat məlumatına əsaslanmır.</summary>
    public async Task<DashboardAccess?> ResolveAccessAsync(DashboardScope scope, CancellationToken cancellationToken)
    {
        var role = await db.Users.AsNoTracking().Where(user => user.Id == scope.UserId && user.IsActive)
            .Select(user => (int?)user.RoleId).SingleOrDefaultAsync(cancellationToken);
        if (!role.HasValue || scope.Role == RoleCode.SuperAdmin && role != (int)RoleCode.SuperAdmin) return null;
        var allowed = Restaurants(scope with { RestaurantId = null });
        if (scope.Role != RoleCode.SuperAdmin && !await allowed.AnyAsync(cancellationToken)) return null;
        DashboardRestaurantOption? selected = null;
        if (scope.RestaurantId.HasValue)
        {
            selected = await allowed.Where(restaurant => restaurant.Id == scope.RestaurantId)
                .Select(restaurant => new DashboardRestaurantOption(restaurant.Id, restaurant.Name, restaurant.BranchName, restaurant.TimeZone))
                .SingleOrDefaultAsync(cancellationToken);
            if (selected == null) return null;
        }
        var options = await allowed.OrderBy(restaurant => restaurant.Name).ThenBy(restaurant => restaurant.Id)
            .Select(restaurant => new DashboardRestaurantOption(restaurant.Id, restaurant.Name, restaurant.BranchName, restaurant.TimeZone))
            .Take(DashboardPeriodPolicy.RestaurantOptionLimit + 1).ToListAsync(cancellationToken);
        var more = options.Count > DashboardPeriodPolicy.RestaurantOptionLimit;
        if (more) options.RemoveAt(options.Count - 1);
        if (selected != null && options.All(option => option.Id != selected.Id)) options[^1] = selected;
        return new(options, more, selected?.TimeZone ?? DashboardPeriodPolicy.DefaultTimeZone);
    }
}
