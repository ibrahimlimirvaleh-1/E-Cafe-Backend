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
    /// <summary>Yalnız cari səhifədəki açıq masaların hesab məlumatlarını split query ilə gətirir; hər masa üçün ayrıca sorğu yaratmır.</summary>
    public async Task<DashboardPage<Domain.Entities.TableSession>> GetSessionsAsync(DashboardScope scope, int pageIndex, CancellationToken cancellationToken)
    {
        var query = OpenSessions(scope);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.AsNoTrackingWithIdentityResolution().OrderBy(session => session.OpenedAt).ThenBy(session => session.Id)
            .Skip((pageIndex - 1) * DashboardPeriodPolicy.PageSize).Take(DashboardPeriodPolicy.PageSize)
            .Include(session => session.Table).Include(session => session.WaiterUser)
            .Include(session => session.Orders).ThenInclude(order => order.OrderItems)
            .Include(session => session.Payments)
            .Include(session => session.Reservation).ThenInclude(reservation => reservation!.Payments)
            .Include(session => session.Reservation).ThenInclude(reservation => reservation!.PaymentProofs)
            .Include(session => session.Reservation).ThenInclude(reservation => reservation!.Refunds)
            .Include(session => session.Reservation).ThenInclude(reservation => reservation!.TableSessions)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new(rows, count);
    }
}
