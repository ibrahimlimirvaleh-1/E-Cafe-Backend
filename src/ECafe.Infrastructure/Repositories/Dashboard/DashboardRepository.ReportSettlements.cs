using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Seçilmiş dövrdə yaradılmış gözləyən/uğursuz/yoxlama tələb edən ödəniş və tamamlanmamış refundları göstərir; cəhd məbləğlərini borc və ya daxilolma kimi toplamır.</summary>
    private async Task<DashboardReportData> ReadSettlementReportAsync(DashboardScope scope, DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var pending = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending);
        var failed = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Failed);
        var paidRefund = StatusIds.Refund(RefundStatus.Refunded);
        var rejectedRefund = StatusIds.Refund(RefundStatus.Rejected);
        var payments = db.Payments.AsNoTracking().Where(p => ids.Contains(p.RestaurantId) && p.IsTest == scope.IsTest && p.CurrencyCode == TableSessionBillingPolicy.Currency &&
            p.CreatedAt >= period.StartUtc && p.CreatedAt < period.EndUtc && (p.PaymentStatusId == pending || p.PaymentStatusId == failed || p.RequiresRefund))
            .Select(p => new { p.Id, Event = "payment", p.RestaurantId, Restaurant = p.Restaurant.Name, At = p.CreatedAt,
                p.Amount, Status = p.PaymentStatusId, p.ReservationId, p.TableSessionId, Review = p.RequiresRefund });
        var refunds = db.ReservationRefunds.AsNoTracking().Where(r => ids.Contains(r.Reservation.RestaurantId) && r.SourcePayment != null && !r.SourcePayment.IsDeleted &&
            r.SourcePayment.RestaurantId == r.Reservation.RestaurantId && r.SourcePayment.IsTest == scope.IsTest && r.CurrencyCode == TableSessionBillingPolicy.Currency &&
            r.RequestedAt >= period.StartUtc && r.RequestedAt < period.EndUtc && r.StatusId != paidRefund && r.StatusId != rejectedRefund)
            .Select(r => new { r.Id, Event = "refund", r.Reservation.RestaurantId, Restaurant = r.Reservation.Restaurant.Name, At = r.RequestedAt,
                r.Amount, Status = r.StatusId, ReservationId = (int?)r.ReservationId, r.SourcePayment!.TableSessionId, Review = true });
        var query = payments.Concat(refunds);
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var rows = await query.OrderByDescending(r => r.At).ThenBy(r => r.Event).ThenByDescending(r => r.Id).Skip(page.Skip).Take(page.Take).ToListAsync(token);
        return View.Map(count, "event:code recordId:number restaurant requestedAt:datetime status:code requestedAmount:currency reservationId:number sessionId:number review:code", rows.Select(row =>
            View.Row($"{row.Event}:{row.Id}", row.Event, row.Id, row.Restaurant, row.At, row.Event + "." + row.Status, row.Amount, row.ReservationId, row.TableSessionId, row.Review)),
            View.Metric("paymentIssues", await payments.CountAsync(token)), View.Metric("refundIssues", await refunds.CountAsync(token)));
    }
}
