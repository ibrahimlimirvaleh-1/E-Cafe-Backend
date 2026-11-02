using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Planlaşdırılmış gəliş tarixinə görə rezervasiyaları qaytarır; tələb olunan depoziti faktiki pul daxilolması kimi təqdim etmir.</summary>
    private async Task<DashboardReportData> ReadReservationReportAsync(DashboardScope scope, DashboardPeriod period,
        int? statusId, int pageIndex, bool export, CancellationToken cancellationToken)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var query = db.Reservations.AsNoTracking().Where(reservation => ids.Contains(reservation.RestaurantId) &&
            reservation.ReservedAt >= period.StartUtc && reservation.ReservedAt < period.EndUtc &&
            (!statusId.HasValue || reservation.StatusId == statusId));
        var totals = await query.GroupBy(_ => 1).Select(group => new DashboardReportTotals
        {
            RowCount = group.Count(), GuestCount = group.Sum(reservation => reservation.PeopleCount),
            ArrivedCount = group.Count(reservation => reservation.ArrivedAt != null),
            SeatedCount = group.Count(reservation => reservation.SeatedAt != null)
        }).SingleOrDefaultAsync(cancellationToken) ?? new DashboardReportTotals();
        var page = ReportPage(totals.RowCount, pageIndex, export);
        var rows = await query.OrderByDescending(reservation => reservation.ReservedAt).ThenByDescending(reservation => reservation.Id)
            .Skip(page.Skip).Take(page.Take).Select(reservation => new DashboardReservationReportRow(
                reservation.Id, reservation.RestaurantId, reservation.Restaurant.Name,
                string.IsNullOrWhiteSpace(reservation.Table.Name) ? "Masa-" + reservation.Table.TableNo : reservation.Table.Name,
                reservation.StatusId, reservation.ReservedAt, reservation.PeopleCount, reservation.DepositAmount,
                reservation.ArrivedAt, reservation.SeatedAt)).ToListAsync(cancellationToken);
        return new(totals, [], rows, []);
    }
}
