using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Dövrə kəsişən sessiyaların doluluq dəqiqələrini sərhəddə kəsir; cari borcu isə mövcud billing siyasəti ilə hesablayıb tarixə aid qalıq kimi təqdim etmir.</summary>
    private async Task<DashboardReportData> ReadTableUsageReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var end = period.EndUtc < now ? period.EndUtc : now;
        var sessions = db.TableSessions.AsNoTracking().Where(s => ids.Contains(s.RestaurantId) && s.Table.RestaurantId == s.RestaurantId &&
            s.OpenedAt < end && (s.ClosedAt == null || s.ClosedAt > period.StartUtc));
        var query = sessions.GroupBy(s => new { s.RestaurantId, Restaurant = s.Restaurant.Name, s.TableId, Name = s.Table.Name ?? "Masa-" + s.Table.TableNo })
            .Select(group => new { group.Key.RestaurantId, group.Key.Restaurant, group.Key.TableId, group.Key.Name,
                Visits = group.Count(s => s.OpenedAt >= period.StartUtc), Closed = group.Count(s => s.ClosedAt != null && s.ClosedAt <= end),
                Minutes = group.Sum(s => s.ClosedAt < s.OpenedAt ? 0 : ((s.ClosedAt != null && s.ClosedAt < end ? s.ClosedAt.Value : end) - (s.OpenedAt > period.StartUtc ? s.OpenedAt : period.StartUtc)).TotalMinutes),
                Invalid = group.Any(s => s.ClosedAt < s.OpenedAt),
                Average = group.Average(s => s.ClosedAt >= s.OpenedAt ? (double?)(s.ClosedAt!.Value - s.OpenedAt).TotalMinutes : null) });
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var rows = await query.OrderBy(r => r.RestaurantId).ThenBy(r => r.TableId).Skip(page.Skip).Take(page.Take).ToListAsync(token);
        var tableIds = rows.Select(r => r.TableId).ToArray();
        var live = await OpenSessions(scope).AsNoTrackingWithIdentityResolution().Where(s => tableIds.Contains(s.TableId)).Include(s => s.Table)
            .Include(s => s.Orders).ThenInclude(o => o.OrderItems).Include(s => s.Payments)
            .Include(s => s.Reservation).ThenInclude(r => r!.Payments).Include(s => s.Reservation).ThenInclude(r => r!.PaymentProofs)
            .Include(s => s.Reservation).ThenInclude(r => r!.Refunds).Include(s => s.Reservation).ThenInclude(r => r!.TableSessions)
            .AsSplitQuery().ToListAsync(token);
        var bills = live.Select(DashboardSessionMapper.Map).GroupBy(r => r.TableId).ToDictionary(g => g.Key, g => g.ToList());
        return View.Map(count, "restaurant table visits:number closedVisits:number occupiedMinutes:minutes averageSessionMinutes:minutes liveSessionId:number remainingAmount:currency review:code", rows.Select(row =>
        {
            var entries = bills.GetValueOrDefault(row.TableId);
            var bill = entries?.Count == 1 ? entries[0] : null;
            var review = row.Invalid || entries?.Count > 1 || bill?.RequiresReview == true;
            return View.Row($"{row.RestaurantId}:{row.TableId}", row.Restaurant, row.Name, row.Visits, row.Closed, Math.Max(0, row.Minutes), row.Average,
                bill?.SessionId, review ? null : bill?.RemainingAmount, review);
        }), View.Metric("tablesUsed", count), View.Metric("visits", await sessions.CountAsync(s => s.OpenedAt >= period.StartUtc, token)));
    }
}
