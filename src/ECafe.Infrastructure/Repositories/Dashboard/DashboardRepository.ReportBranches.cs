using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Filialları eyni interval və pul rejimində müqayisə edir; ilkin depozit və son hesab daxilolmasını sifariş məbləğindən ayrı saxlayır.</summary>
    private async Task<DashboardReportData> ReadBranchComparisonReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var restaurants = Restaurants(scope);
        var count = await restaurants.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var pending = StatusIds.Build(StatusType.Order, (int)OrderStatus.PendingWaiterConfirmation);
        var receipts = Receipts(scope);
        var refunds = Refunds(scope);
        var rows = await restaurants.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip(page.Skip).Take(page.Take).Select(r => new
        {
            r.Id, r.Name,
            Reservations = r.Reservations.Count(x => x.ReservedAt >= period.StartUtc && x.ReservedAt < period.EndUtc),
            Orders = r.Orders.Count(x => x.OpenedAt >= period.StartUtc && x.OpenedAt < period.EndUtc && x.StatusId != cancelled && x.StatusId != pending),
            Cancelled = r.Orders.Count(x => x.CancelledAt >= period.StartUtc && x.CancelledAt < period.EndUtc && x.StatusId == cancelled),
            Amount = r.Orders.Where(x => x.OpenedAt >= period.StartUtc && x.OpenedAt < period.EndUtc && x.StatusId != cancelled && x.StatusId != pending)
                .SelectMany(x => x.OrderItems).Where(x => !x.IsDeleted).Sum(x => (decimal?)x.LineTotal) ?? 0,
            Collected = receipts.Where(x => x.RestaurantId == r.Id && x.PaidAt >= period.StartUtc && x.PaidAt < period.EndUtc).Sum(x => (decimal?)x.Amount) ?? 0,
            Refunded = refunds.Where(x => x.Reservation.RestaurantId == r.Id && x.RefundedAt >= period.StartUtc && x.RefundedAt < period.EndUtc).Sum(x => (decimal?)x.Amount) ?? 0,
            AveragePreparation = r.Orders.Where(x => x.ReadyAt >= period.StartUtc && x.ReadyAt < period.EndUtc && x.ReadyAt >= x.PreparingAt)
                .Average(x => (double?)(x.ReadyAt!.Value - x.PreparingAt!.Value).TotalMinutes)
        }).ToListAsync(token);
        return View.Map(count, "restaurant reservations:number confirmedOrders:number cancelled:number confirmedAmount:currency collected:currency refunded:currency netCollected:currency averageOrder:currency preparationMinutes:minutes", rows.Select(row =>
            View.Row(row.Id.ToString(), row.Name, row.Reservations, row.Orders, row.Cancelled, row.Amount, row.Collected, row.Refunded, row.Collected - row.Refunded,
                row.Orders > 0 ? decimal.Round(row.Amount / row.Orders, 2) : (decimal?)null, row.AveragePreparation)), View.Metric("branches", count));
    }
}
