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
    /// <summary>Ödənilmiş və sonradan qaytarılmış provayder ödənişlərini ilkin pul daxilolması kimi seçir; test və real rejimləri ayırır.</summary>
    private IQueryable<Payment> Receipts(DashboardScope scope)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var paid = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid);
        var refunded = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded);
        return db.Payments.AsNoTracking().Where(payment => ids.Contains(payment.RestaurantId) && payment.IsTest == scope.IsTest &&
            payment.PaidAt.HasValue && payment.Amount > 0 && payment.CurrencyCode == TableSessionBillingPolicy.Currency &&
            payment.ProviderName != null && payment.ProviderName != "" && payment.ProviderRef != null && payment.ProviderRef != "" &&
            (payment.PaymentStatusId == paid || payment.PaymentStatusId == refunded));
    }

    /// <summary>Refundu yalnız tamamlanma tarixində və mənbə ödənişinin restoran/test aidiyyəti uyğun olduqda sayır.</summary>
    private IQueryable<Domain.Entities.ReservationRefund> Refunds(DashboardScope scope)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var status = StatusIds.Refund(RefundStatus.Refunded);
        return db.ReservationRefunds.AsNoTracking().Where(refund => ids.Contains(refund.Reservation.RestaurantId) &&
            refund.StatusId == status && refund.RefundedAt.HasValue && refund.Amount > 0 &&
            refund.CurrencyCode == TableSessionBillingPolicy.Currency && refund.SourcePayment != null &&
            refund.SourcePayment.IsTest == scope.IsTest && refund.SourcePayment.RestaurantId == refund.Reservation.RestaurantId &&
            refund.SourcePayment.PaidAt.HasValue && !refund.SourcePayment.IsDeleted);
    }

    /// <summary>Sifariş snapshot məbləğlərini və pul daxilolmalarını ayrıca toplayır; depoziti sifariş cəminə əlavə etmir.</summary>
    public async Task<DashboardTotals> GetTotalsAsync(DashboardScope scope, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var ids = Restaurants(scope).Select(restaurant => restaurant.Id);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var orderQuery = db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) &&
            order.OpenedAt >= startUtc && order.OpenedAt < endUtc && order.StatusId != cancelled);
        var orders = await orderQuery.GroupBy(_ => 1).Select(group => new
        {
            Count = group.Count(), Amount = group.Sum(order => order.OrderItems.Where(line => !line.IsDeleted).Sum(line => (decimal?)line.LineTotal) ?? 0)
        }).SingleOrDefaultAsync(cancellationToken);
        var reservations = await db.Reservations.AsNoTracking().CountAsync(reservation => ids.Contains(reservation.RestaurantId) &&
            reservation.ReservedAt >= startUtc && reservation.ReservedAt < endUtc, cancellationToken);
        var receipts = await Receipts(scope).Where(payment => payment.PaidAt >= startUtc && payment.PaidAt < endUtc)
            .GroupBy(_ => 1).Select(group => new
            {
                Total = group.Sum(payment => payment.Amount),
                Deposit = group.Sum(payment => payment.ReservationId != null ? payment.Amount : 0),
                Session = group.Sum(payment => payment.TableSessionId != null && payment.ReservationId == null ? payment.Amount : 0)
            }).SingleOrDefaultAsync(cancellationToken);
        var refunds = await Refunds(scope).Where(refund => refund.RefundedAt >= startUtc && refund.RefundedAt < endUtc)
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0;
        return new(orders?.Count ?? 0, orders?.Amount ?? 0, reservations, receipts?.Total ?? 0,
            receipts?.Deposit ?? 0, receipts?.Session ?? 0, refunds);
    }
}
