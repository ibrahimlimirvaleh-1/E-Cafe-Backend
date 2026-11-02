using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Mətbəxə göndərilmə dövrü üzrə tamamlanmış mərhələlərin faktiki müddətini hesablayır; olmayan və mənfi intervalı sıfır saymır.</summary>
    private async Task<DashboardReportData> ReadKitchenReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var query = db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) && order.SentToKitchenAt >= period.StartUtc && order.SentToKitchenAt < period.EndUtc)
            .Select(order => new { order.Id, Restaurant = order.Restaurant.Name, Table = order.Table.Name ?? "Masa-" + order.Table.TableNo, order.StatusId, order.SentToKitchenAt,
                Wait = order.AcceptedAt >= order.SentToKitchenAt ? (double?)(order.AcceptedAt!.Value - order.SentToKitchenAt!.Value).TotalMinutes : null,
                Prepare = order.ReadyAt >= order.PreparingAt ? (double?)(order.ReadyAt!.Value - order.PreparingAt!.Value).TotalMinutes : null,
                Serve = order.ServedAt >= order.ReadyAt ? (double?)(order.ServedAt!.Value - order.ReadyAt!.Value).TotalMinutes : null,
                Total = order.ServedAt >= order.SentToKitchenAt ? (double?)(order.ServedAt!.Value - order.SentToKitchenAt!.Value).TotalMinutes : null });
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var stats = await query.GroupBy(_ => 1).Select(group => new { Completed = group.Count(row => row.Total != null),
            Prepare = group.Average(row => row.Prepare), Serve = group.Average(row => row.Serve) }).SingleOrDefaultAsync(token);
        var rows = await query.OrderByDescending(row => row.Total ?? row.Prepare ?? row.Wait ?? -1).ThenByDescending(row => row.SentToKitchenAt).ThenByDescending(row => row.Id).Skip(page.Skip).Take(page.Take).ToListAsync(token);
        var metrics = new List<ECafe.Application.DTOs.Dashboard.DashboardMetric> { View.Metric("kitchenOrders", count), View.Metric("servedOrders", stats?.Completed ?? 0),
            View.Metric("measuredPreparation", await query.CountAsync(row => row.Prepare != null, token)), View.Metric("measuredServing", await query.CountAsync(row => row.Serve != null, token)) };
        if (stats?.Prepare is double preparation) metrics.Add(View.Metric("averagePreparation", decimal.Round((decimal)preparation, 2), "minutes"));
        if (stats?.Serve is double serving) metrics.Add(View.Metric("averageServing", decimal.Round((decimal)serving, 2), "minutes"));
        return View.Map(count, "orderId:number restaurant table status:code sentAt:datetime acceptanceMinutes:minutes preparationMinutes:minutes servingMinutes:minutes totalMinutes:minutes", rows.Select(row =>
            View.Row(row.Id.ToString(), row.Id, row.Restaurant, row.Table, "order." + row.StatusId, row.SentToKitchenAt, row.Wait, row.Prepare, row.Serve, row.Total)),
            metrics.ToArray());
    }
}
