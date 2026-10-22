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
    /// <summary>Canlı vəziyyəti tarix qrafikindən ayrıca hesablayır; köhnə açıq sifarişləri və gözləyən qonaqları gizlətmir.</summary>
    public async Task<DashboardLive> GetLiveAsync(DashboardScope scope, DateTime nowUtc, DateTime todayStartUtc, DateTime todayEndUtc, CancellationToken cancellationToken)
    {
        var restaurants = Restaurants(scope);
        var ids = restaurants.Select(restaurant => restaurant.Id);
        var activeRestaurants = await restaurants.CountAsync(restaurant => restaurant.IsActive, cancellationToken);
        var activeUsers = scope.Role == RoleCode.SuperAdmin
            ? await db.Users.AsNoTracking().CountAsync(user => user.IsActive, cancellationToken) : 0;
        var contractActive = StatusIds.Contract(ContractStatus.Active);
        var warningEnd = nowUtc.AddDays(DashboardPeriodPolicy.ContractWarningDays);
        var contracts = await db.RestaurantContracts.AsNoTracking().Where(contract => ids.Contains(contract.RestaurantId) &&
            contract.StatusId == contractActive && contract.StartDate <= nowUtc && (contract.EndDate == null || contract.EndDate > nowUtc))
            .GroupBy(_ => 1).Select(group => new
            {
                Active = group.Count(), Expiring = group.Count(contract => contract.EndDate != null && contract.EndDate <= warningEnd)
            }).SingleOrDefaultAsync(cancellationToken);
        var confirmed = StatusIds.Reservation(ReservationStatus.Confirmed);
        var todayReservations = scope.Role == RoleCode.Manager ? await db.Reservations.AsNoTracking().CountAsync(reservation =>
            ids.Contains(reservation.RestaurantId) && reservation.ReservedAt >= todayStartUtc && reservation.ReservedAt < todayEndUtc, cancellationToken) : 0;
        var waiting = scope.Role == RoleCode.SuperAdmin ? 0 : await db.Reservations.AsNoTracking().CountAsync(reservation =>
            ids.Contains(reservation.RestaurantId) && reservation.StatusId == confirmed && reservation.ArrivedAt != null &&
            reservation.SeatedAt == null && reservation.CompletedAt == null && reservation.CancelledAt == null, cancellationToken);
        var sessions = await OpenSessions(scope).GroupBy(_ => 1).Select(group => new
        {
            Open = group.Count(), Review = group.Count(session => session.CloseReviewDueAt != null && session.CloseReviewDueAt <= nowUtc &&
                session.CloseReviewConfirmedAt == null)
        }).SingleOrDefaultAsync(cancellationToken);
        var sessionIds = OpenSessions(scope).Select(session => session.Id);
        var created = StatusIds.Build(StatusType.Order, (int)OrderStatus.Created);
        var accepted = StatusIds.Build(StatusType.Order, (int)OrderStatus.Accepted);
        var preparing = StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing);
        var ready = StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready);
        var delayStart = nowUtc.AddMinutes(-DashboardPeriodPolicy.KitchenDelayMinutes);
        var orders = await db.Orders.AsNoTracking().Where(order => ids.Contains(order.RestaurantId) && order.ClosedAt == null &&
                order.TableSessionId.HasValue && sessionIds.Contains(order.TableSessionId.Value))
            .GroupBy(_ => 1).Select(group => new
            {
                Kitchen = group.Count(order => order.SentToKitchenAt != null && (order.StatusId == created || order.StatusId == accepted || order.StatusId == preparing)),
                Ready = group.Count(order => order.StatusId == ready && order.ReadyAt != null && order.ServedAt == null),
                Delayed = group.Count(order => order.StatusId == preparing && order.PreparingAt != null && order.PreparingAt < delayStart)
            }).SingleOrDefaultAsync(cancellationToken);
        var low = await db.InventoryItems.AsNoTracking().CountAsync(item => ids.Contains(item.RestaurantId) && item.IsActive &&
            item.QuantityOnHand <= item.LowStockThreshold, cancellationToken);
        var failedStatus = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Failed);
        var recentStart = nowUtc.AddHours(-24);
        var failed = scope.Role == RoleCode.Manager ? 0 : await db.Payments.AsNoTracking().CountAsync(payment => ids.Contains(payment.RestaurantId) &&
            payment.IsTest == scope.IsTest && payment.PaymentStatusId == failedStatus && payment.CreatedAt >= recentStart, cancellationToken);
        var review = scope.Role == RoleCode.Manager ? 0 : await db.Payments.AsNoTracking().CountAsync(payment => ids.Contains(payment.RestaurantId) &&
            payment.IsTest == scope.IsTest && payment.RequiresRefund, cancellationToken);
        var refunded = StatusIds.Refund(RefundStatus.Refunded);
        var rejected = StatusIds.Refund(RefundStatus.Rejected);
        var pendingRefund = scope.Role == RoleCode.Manager ? 0 : await db.ReservationRefunds.AsNoTracking().CountAsync(refund =>
            ids.Contains(refund.Reservation.RestaurantId) && refund.StatusId != refunded && refund.StatusId != rejected &&
            refund.SourcePayment != null && !refund.SourcePayment.IsDeleted && refund.SourcePayment.IsTest == scope.IsTest &&
            refund.SourcePayment.RestaurantId == refund.Reservation.RestaurantId, cancellationToken);
        return new(activeRestaurants, activeUsers, contracts?.Active ?? 0, todayReservations, sessions?.Open ?? 0,
            orders?.Kitchen ?? 0, orders?.Ready ?? 0, orders?.Delayed ?? 0, waiting, low,
            contracts?.Expiring ?? 0, failed, pendingRefund, review, sessions?.Review ?? 0);
    }
}
