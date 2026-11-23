using ECafe.Application.Repositories.User;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.User;

public sealed class CustomerAccountClosureRepository(ECafeDbContext db) : ICustomerAccountClosureRepository
{
    public async Task<IReadOnlyList<CustomerAccountClosureBlocker>> GetBlockersAsync(int userId, CancellationToken cancellationToken)
    {
        var result = new List<CustomerAccountClosureBlocker>();
        if (await db.UserRestaurants.AnyAsync(value => value.UserId == userId && value.IsActive &&
            UserProfileAccessPolicy.RestaurantRoleIds.Contains(value.RoleId), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.ActiveStaffProfile);
        var terminalReservations = new[] { ReservationStatus.Completed, ReservationStatus.Cancelled, ReservationStatus.NoShow,
            ReservationStatus.Expired, ReservationStatus.Rejected }.Select(StatusIds.Reservation).ToArray();
        if (await db.Reservations.AnyAsync(value => value.CustomerUserId == userId && !terminalReservations.Contains(value.StatusId), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.ActiveReservation);
        var closed = StatusIds.TableSession(TableSessionStatus.Closed);
        var cancelled = StatusIds.TableSession(TableSessionStatus.Cancelled);
        if (await db.TableSessions.AnyAsync(value => value.CustomerUserId == userId &&
            (value.ClosedAt == null || value.StatusId != closed && value.StatusId != cancelled), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.OpenTableSession);
        var pending = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending);
        if (await db.Payments.AnyAsync(value => (value.CreatedByUserId == userId ||
            value.Reservation != null && value.Reservation.CustomerUserId == userId ||
            value.TableSession != null && value.TableSession.CustomerUserId == userId ||
            value.Order != null && value.Order.CustomerUserId == userId) &&
            (value.PaymentStatusId == pending || value.RequiresRefund && value.RefundedAt == null), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.PendingPayment);
        var refunded = StatusIds.Refund(RefundStatus.Refunded);
        var rejected = StatusIds.Refund(RefundStatus.Rejected);
        var paid = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid);
        var confirmedProof = StatusIds.Reservation(ReservationStatus.Confirmed);
        if (await db.ReservationRefunds.AnyAsync(value => value.Reservation.CustomerUserId == userId &&
            value.StatusId != refunded && value.StatusId != rejected, cancellationToken) ||
            await db.Reservations.AnyAsync(value => value.CustomerUserId == userId && value.RefundEligible == true &&
                !value.Refunds.Any(refund => refund.StatusId == refunded || refund.StatusId == rejected) &&
                (value.Payments.Any(payment => payment.PaymentStatusId == paid && payment.Amount > 0 && payment.RefundedAt == null) ||
                 value.PaymentProofs.Any(proof => proof.StatusId == confirmedProof && proof.Amount > 0)), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.PendingRefund);
        if (await db.Wallets.AnyAsync(value => value.UserId == userId && value.Balance != 0, cancellationToken))
            result.Add(CustomerAccountClosureBlocker.WalletBalance);
        var orderCancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var served = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served);
        var orderClosed = StatusIds.Build(StatusType.Order, (int)OrderStatus.Closed);
        if (await db.Orders.AnyAsync(value => value.CustomerUserId == userId && value.StatusId != orderCancelled &&
            (value.TableSessionId == null || value.StatusId != served && value.StatusId != orderClosed), cancellationToken))
            result.Add(CustomerAccountClosureBlocker.UnresolvedOrder);
        if (await HasUnsettledBillAsync(userId, cancellationToken)) result.Add(CustomerAccountClosureBlocker.UnpaidBill);
        return result;
    }

    public async Task RevokeRecoveryAndPushAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var value in await db.EmailChangeRequests.Where(value => value.UserId == userId && value.UsedAt == null && value.RevokedAt == null).ToListAsync(cancellationToken))
            value.RevokedAt = nowUtc;
        foreach (var value in await db.PasswordResetTokens.Where(value => value.UserId == userId && value.UsedAt == null && value.RevokedAt == null).ToListAsync(cancellationToken))
            value.RevokedAt = nowUtc;
        foreach (var value in await db.UserPasswordSetupTokens.Where(value => value.UserId == userId && value.UsedAt == null).ToListAsync(cancellationToken))
            value.UsedAt = nowUtc;
        foreach (var value in await db.MobilePushInstallations.Where(value => value.UserId == userId &&
            (value.IsActive || value.ProtectedToken != string.Empty)).ToListAsync(cancellationToken))
        {
            value.IsActive = false;
            value.DeactivatedAt = nowUtc;
            value.ProtectedToken = string.Empty;
        }
    }

    /// <summary>Köhnə bağlanmış borclu sessiyaları da ortaq hesab qaydası ilə yoxlayır; yaddaşı 50 sessiyalı səhifələrlə məhdudlaşdırır.</summary>
    private async Task<bool> HasUnsettledBillAsync(int userId, CancellationToken cancellationToken)
    {
        var lastId = 0;
        while (true)
        {
            var page = await db.TableSessions.AsNoTrackingWithIdentityResolution()
                .Where(value => value.CustomerUserId == userId && value.Id > lastId).OrderBy(value => value.Id).Take(50)
                .Include(value => value.Orders).ThenInclude(value => value.OrderItems)
                .Include(value => value.Payments)
                .Include(value => value.Reservation).ThenInclude(value => value!.Payments)
                .Include(value => value.Reservation).ThenInclude(value => value!.PaymentProofs)
                .Include(value => value.Reservation).ThenInclude(value => value!.Refunds)
                .Include(value => value.Reservation).ThenInclude(value => value!.TableSessions)
                .AsSplitQuery().ToListAsync(cancellationToken);
            if (page.Count == 0) return false;
            foreach (var session in page)
            {
                try
                {
                    var bill = TableSessionBillingPolicy.Calculate(session);
                    if (bill.RemainingAmount > 0 || bill.RequiresReview || bill.HasPendingPayment || bill.HasUnservedOrders) return true;
                }
                catch (BusinessRuleException) { return true; }
            }
            lastId = page[^1].Id;
        }
    }
}
