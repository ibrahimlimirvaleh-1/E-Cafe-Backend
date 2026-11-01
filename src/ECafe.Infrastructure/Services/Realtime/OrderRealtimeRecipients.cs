using ECafe.Application.Services.Realtime;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Services.Realtime;

public sealed class OrderRealtimeRecipients(ECafeDbContext context)
{
    /// <summary>Çatdırılma anındakı canlı təyinatları yoxlayır; başqa restoranı və keçmiş ofisiantı auditoriyaya daxil etmir.</summary>
    public async Task<int[]> ResolveAsync(OrderRealtimePayload payload, CancellationToken token)
    {
        if (!await context.Restaurants.AnyAsync(item => item.Id == payload.RestaurantId && item.IsActive, token)) return [];
        if (payload.OrderId.HasValue && !await context.Orders.IgnoreQueryFilters().AnyAsync(order =>
                order.Id == payload.OrderId && order.RestaurantId == payload.RestaurantId &&
                order.TableSessionId == payload.TableSessionId, token)) return [];
        if (payload.TableSessionId.HasValue && !await context.TableSessions.AnyAsync(session =>
                session.Id == payload.TableSessionId && session.RestaurantId == payload.RestaurantId, token)) return [];
        int? waiter = null, customer = null;
        var session = payload.TableSessionId.HasValue ? await context.TableSessions.AsNoTracking()
            .Where(item => item.Id == payload.TableSessionId && item.RestaurantId == payload.RestaurantId)
            .Select(item => new { item.WaiterUserId, item.CustomerUserId }).SingleOrDefaultAsync(token) : null;
        if (session != null) { waiter = session.WaiterUserId; customer = session.CustomerUserId; }
        else if (payload.TableSessionId == null && payload.OrderId.HasValue)
        {
            var order = await context.Orders.AsNoTracking().Where(item => item.Id == payload.OrderId &&
                item.RestaurantId == payload.RestaurantId && item.TableSessionId == null)
                .Select(item => new { item.WaiterUserId, item.CustomerUserId }).SingleOrDefaultAsync(token);
            waiter = order?.WaiterUserId; customer = order?.CustomerUserId;
        }
        var staff = await context.UserRestaurants.AsNoTracking().Where(item => item.RestaurantId == payload.RestaurantId &&
                item.IsActive && item.User.IsActive && (item.UserId == waiter && item.RoleId == (int)RoleCode.Waiter ||
                    payload.IncludeKitchen && item.RoleId == (int)RoleCode.Kitchen))
            .Select(item => item.UserId).Distinct().ToListAsync(token);
        if (customer.HasValue && await context.Users.AnyAsync(item => item.Id == customer && item.IsActive, token)) staff.Add(customer.Value);
        return staff.Distinct().ToArray();
    }
}
