using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Daxilolma və tamamlanmış refund hadisələrini öz tarixlərində birləşdirir; eyni mənbə ödənişini iki daxilolma kimi saymır.</summary>
    private async Task<DashboardReportData> ReadPaymentReportAsync(DashboardScope scope, DashboardPeriod period,
        string paymentEvent, int pageIndex, bool export, CancellationToken cancellationToken)
    {
        var receipts = Receipts(scope).Where(payment => payment.PaidAt >= period.StartUtc && payment.PaidAt < period.EndUtc)
            .Select(payment => new
            {
                payment.Id, Event = "receipt", payment.RestaurantId, RestaurantName = payment.Restaurant.Name,
                OccurredAtUtc = payment.PaidAt!.Value, payment.Amount, Purpose = payment.ReservationId != null ? "deposit" :
                    payment.TableSessionId != null ? "session" : payment.OrderId != null ? "order" : "other",
                payment.ReservationId, payment.TableSessionId, StatusId = payment.PaymentStatusId, payment.IsTest
            });
        var refunds = Refunds(scope).Where(refund => refund.RefundedAt >= period.StartUtc && refund.RefundedAt < period.EndUtc)
            .Select(refund => new
            {
                refund.Id, Event = "refund", refund.Reservation.RestaurantId, RestaurantName = refund.Reservation.Restaurant.Name,
                OccurredAtUtc = refund.RefundedAt!.Value, refund.Amount, Purpose = "deposit",
                ReservationId = (int?)refund.ReservationId, refund.SourcePayment!.TableSessionId, refund.StatusId, refund.SourcePayment.IsTest
            });
        var query = receipts.Concat(refunds);
        if (paymentEvent != "all") query = query.Where(row => row.Event == paymentEvent);
        var totals = await query.GroupBy(_ => 1).Select(group => new DashboardReportTotals
        {
            RowCount = group.Count(), ReceiptCount = group.Count(row => row.Event == "receipt"),
            RefundCount = group.Count(row => row.Event == "refund"),
            CollectedAmount = group.Sum(row => row.Event == "receipt" ? row.Amount : 0),
            RefundedAmount = group.Sum(row => row.Event == "refund" ? row.Amount : 0)
        }).SingleOrDefaultAsync(cancellationToken) ?? new DashboardReportTotals();
        var page = ReportPage(totals.RowCount, pageIndex, export);
        var rows = await query.OrderByDescending(row => row.OccurredAtUtc).ThenBy(row => row.Event).ThenByDescending(row => row.Id)
            .Skip(page.Skip).Take(page.Take).Select(row => new DashboardPaymentReportRow(row.Id, row.Event,
                row.RestaurantId, row.RestaurantName, row.OccurredAtUtc, row.Amount, row.Purpose,
                row.ReservationId, row.TableSessionId, row.StatusId, row.IsTest)).ToListAsync(cancellationToken);
        return new(totals, [], [], rows);
    }
}
