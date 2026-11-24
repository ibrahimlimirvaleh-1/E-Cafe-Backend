using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Outbox;
using ECafe.Application.DTOs.Payment;
using ECafe.Application.Features.Commands.Notification.Create;
using ECafe.Application.Mappings;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Concrete;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Concrete;
using ECafe.Application.Services.Payments;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Application.Services.Payments.Concrete;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.ReservationRefund.Concrete;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Notification;
using ECafe.Infrastructure.Repositories.Reservation;
using ECafe.Infrastructure.Repositories.ReservationRefund;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Services;
using ECafe.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed class StockPostgresTheoryAttribute : TheoryAttribute
{
    /// <summary>Parametrli real DB sınaqlarını da yalnız ayrıca lokal PostgreSQL bağlantısı veriləndə işə salır.</summary>
    public StockPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECAFE_STOCK_TEST_CONNECTION")))
            Skip = "Set ECAFE_STOCK_TEST_CONNECTION to an isolated local PostgreSQL cluster.";
    }
}

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task PaymentPgSameDepositKeyAcrossConnectionsCreatesOneCheckoutAndAudit()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var key = Guid.NewGuid().ToString("D");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await PaymentPgScope(db).Provider.CreateCheckoutAsync(1001, key, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results.Select(value => value.Id).Distinct());
        await using var read = fixture.Context();
        var payment = Assert.Single(await read.Payments.ToListAsync());
        Assert.Equal(20m, payment.Amount);
        Assert.Equal(key, payment.CheckoutKey);
        Assert.Equal("simulation", payment.ProviderName);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderCheckoutCreated));
    }

    [StockPostgresFact]
    public async Task PaymentPgDifferentDepositKeysCreateOnlyOnePendingAttempt()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await AttemptAsync(async () => { await PaymentPgScope(db).Provider.CreateCheckoutAsync(1001, Guid.NewGuid().ToString("D"), default); });
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(attempts, error => error == null);
        Assert.All(attempts.Where(error => error != null), error =>
            Assert.Equal(ErrorCode.PaymentRequestKeyConflict, Assert.IsType<BusinessRuleException>(error).Code));
        await using var read = fixture.Context();
        Assert.Single(await read.Payments.ToListAsync());
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderCheckoutCreated));
    }

    [StockPostgresFact]
    public async Task PaymentPgSameKeyCannotBeSpentAtTwoRestaurantsInParallel()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        await using (var setup = fixture.Context())
        {
            setup.Reservations.Add(PaymentPgReservation(1002, 1002, 1002));
            await setup.SaveChangesAsync();
        }
        var key = Guid.NewGuid().ToString("D");
        var attempts = await Task.WhenAll(new[] { 1001, 1002 }.Select(async id =>
        {
            await using var db = fixture.Context();
            return await AttemptAsync(async () => { await PaymentPgScope(db).Provider.CreateCheckoutAsync(id, key, default); });
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(attempts, error => error == null);
        Assert.Equal(ErrorCode.PaymentRequestKeyConflict,
            Assert.IsType<BusinessRuleException>(Assert.Single(attempts, error => error != null)).Code);
        await using var read = fixture.Context();
        Assert.Single(await read.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task PaymentPgParallelDepositSuccessConfirmsAndNotifiesExactlyOnce()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var paymentId = await PaymentPgCheckoutAsync(fixture);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await PaymentPgScope(db).Provider.SimulateAsync(paymentId, ProviderOperationStatus.Succeeded, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results.Select(value => value.PaidAt?.UtcTicks / TimeSpan.TicksPerMicrosecond).Distinct());
        await using var read = fixture.Context();
        var reservation = await read.Reservations.Include(value => value.StatusHistory).SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), reservation.StatusId);
        Assert.Single(reservation.StatusHistory);
        Assert.Equal(2, await read.Notifications.CountAsync());
        Assert.Equal(2, await read.OutboxEvents.CountAsync(value => value.EventType == OutboxEventTypes.MobilePushRequested));
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderPaymentUpdated));
    }

    [StockPostgresFact]
    public async Task PaymentPgParallelRefundRequestsCreateOneRefundAndOneHistory()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var paymentId = await PaymentPgPaidCancellationAsync(fixture);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await PaymentPgScope(db).Provider.RequestRefundAsync(1001, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results.Select(value => value.Id).Distinct());
        await using var read = fixture.Context();
        var refund = Assert.Single(await read.ReservationRefunds.Include(value => value.StatusHistory).ToListAsync());
        Assert.Equal(paymentId, refund.SourcePaymentId);
        Assert.Equal(20m, refund.Amount);
        Assert.Single(refund.StatusHistory);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ReservationRefundRequested));
        Assert.Equal(1, await read.Notifications.CountAsync(value => value.TypeId == (int)NotificationType.ReservationRefundRequested));
    }

    [StockPostgresFact]
    public async Task PaymentPgParallelRefundSuccessReturnsMoneyAndNotifiesExactlyOnce()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var paymentId = await PaymentPgPaidCancellationAsync(fixture);
        var refundId = await PaymentPgRefundAsync(fixture);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await PaymentPgScope(db).Provider.SimulateRefundAsync(refundId, ProviderOperationStatus.Succeeded, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results.Select(value => value.RefundedAt?.UtcTicks / TimeSpan.TicksPerMicrosecond).Distinct());
        await using var read = fixture.Context();
        var payment = await read.Payments.SingleAsync(value => value.Id == paymentId);
        var refund = await read.ReservationRefunds.Include(value => value.StatusHistory).SingleAsync();
        Assert.NotNull(payment.RefundedAt);
        Assert.Equal(payment.RefundedAt, refund.RefundedAt);
        Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded), payment.PaymentStatusId);
        Assert.Equal(2, refund.StatusHistory.Count);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderRefundUpdated));
        Assert.Equal(2, await read.Notifications.CountAsync(value => value.TypeId == (int)NotificationType.ReservationRefundTransferConfirmed));
    }

    [StockPostgresFact]
    public async Task PaymentPgSameSessionKeyAcrossConnectionsCreatesOneCheckout()
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill: true);
        var key = Guid.NewGuid().ToString("D");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await PaymentPgScope(db).Bill.CheckoutAsync(1001, key, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(results.Select(value => value.Id).Distinct());
        await using var read = fixture.Context();
        var payment = Assert.Single(await read.Payments.ToListAsync());
        Assert.Equal(7m, payment.Amount);
        Assert.Equal(64, payment.BillFingerprint!.Length);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.SessionBillCheckoutCreated));
    }

    [StockPostgresFact]
    public async Task PaymentPgDifferentActorsAndKeysCannotOpenTwoSessionCheckouts()
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill: true);
        var attempts = await Task.WhenAll(new[] { 1001, 1003, 1004, 1001, 1003, 1004 }.Select(async userId =>
        {
            await using var db = fixture.Context();
            return await AttemptAsync(async () => { await PaymentPgScope(db, userId).Bill.CheckoutAsync(1001, Guid.NewGuid().ToString("D"), default); });
        })).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(attempts, error => error == null);
        Assert.All(attempts.Where(error => error != null), error =>
            Assert.Equal(ErrorCode.SessionBillPaymentPending, Assert.IsType<BusinessRuleException>(error).Code));
        await using var read = fixture.Context();
        Assert.Single(await read.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task PaymentPgParallelSessionSuccessCannotDoubleCreditOrDoubleNotify()
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill: true);
        var paymentId = await PaymentPgCheckoutAsync(fixture, bill: true);
        await Task.WhenAll(new[] { 1001, 1003, 1004, 1001, 1003, 1004 }.Select(async userId =>
        {
            await using var db = fixture.Context();
            await PaymentPgScope(db, userId).Bill.SimulateAsync(1001, paymentId, ProviderOperationStatus.Succeeded, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        await using var read = fixture.Context();
        var bill = TableSessionBillingPolicy.Calculate((await new TableSessionRepository(read).GetForBillingAsync(1001, false, default))!);
        Assert.Equal(7m, bill.PaidAmount);
        Assert.Equal(0m, bill.RemainingAmount);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.SessionBillPaymentUpdated));
        Assert.Equal(3, await read.Notifications.CountAsync(value => value.TypeId == (int)NotificationType.TableSessionPaid));
        Assert.Single(await read.Payments.ToListAsync());
    }

    [StockPostgresTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PaymentPgCheckoutRollsBackInsertedPaymentReferenceAndAuditAndSameKeyCanRetry(bool bill, bool auditFailure)
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill);
        var key = Guid.NewGuid().ToString("D");
        var action = bill ? AuditActions.SessionBillCheckoutCreated : AuditActions.ProviderCheckoutCreated;
        await using (var request = fixture.Context(auditFailure ? new PaymentPgSaveFailure(action) : null))
        {
            var services = PaymentPgScope(request, gateway: auditFailure ? null : new PaymentPgGateway("checkout"));
            var error = await Record.ExceptionAsync(async () =>
            {
                if (bill) await services.Bill.CheckoutAsync(1001, key, default);
                else await services.Provider.CreateCheckoutAsync(1001, key, default);
            });
            Assert.NotNull(error);
            if (auditFailure) Assert.IsType<DbUpdateException>(error);
            else Assert.IsType<IOException>(error);
        }
        await using (var read = fixture.Context())
        {
            Assert.Empty(await read.Payments.ToListAsync());
            Assert.Empty(await read.OutboxEvents.ToListAsync());
            Assert.Empty(await read.Notifications.ToListAsync());
        }
        await using var retry = fixture.Context();
        if (bill) await PaymentPgScope(retry).Bill.CheckoutAsync(1001, key, default);
        else await PaymentPgScope(retry).Provider.CreateCheckoutAsync(1001, key, default);
        Assert.Single(await retry.Payments.ToListAsync());
        Assert.Equal(1, await PaymentPgAuditCountAsync(retry, action));
    }

    [StockPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPgRefundCreationRollsBackAndCanRetryWithoutLosingOriginalPayment(bool auditFailure)
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var paymentId = await PaymentPgPaidCancellationAsync(fixture);
        PaymentPgCounts before;
        await using (var read = fixture.Context()) before = await PaymentPgCountsAsync(read);
        await using (var request = fixture.Context(auditFailure ? new PaymentPgSaveFailure(AuditActions.ReservationRefundRequested) : null))
        {
            var services = PaymentPgScope(request, gateway: auditFailure ? null : new PaymentPgGateway("refund"));
            var error = await Record.ExceptionAsync(() => services.Provider.RequestRefundAsync(1001, default));
            Assert.NotNull(error);
            if (auditFailure) Assert.IsType<DbUpdateException>(error);
            else Assert.IsType<IOException>(error);
        }
        await using (var read = fixture.Context())
        {
            Assert.Equal(before, await PaymentPgCountsAsync(read));
            var payment = await read.Payments.SingleAsync(value => value.Id == paymentId);
            Assert.Null(payment.RefundedAt);
            Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid), payment.PaymentStatusId);
        }
        await PaymentPgRefundAsync(fixture);
        await using var retry = fixture.Context();
        Assert.Single(await retry.ReservationRefunds.ToListAsync());
        Assert.Equal(1, await PaymentPgAuditCountAsync(retry, AuditActions.ReservationRefundRequested));
    }

    [StockPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPgSuccessRollbackIncludesMoneyStatusHistoryNotificationsAndOutboxes(bool bill)
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill);
        var paymentId = await PaymentPgCheckoutAsync(fixture, bill);
        PaymentPgCounts before;
        await using (var read = fixture.Context()) before = await PaymentPgCountsAsync(read);
        await using (var request = fixture.Context(new PaymentPgSaveFailure(notification: true)))
        {
            var services = PaymentPgScope(request);
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                if (bill) await services.Bill.SimulateAsync(1001, paymentId, ProviderOperationStatus.Succeeded, default);
                else await services.Provider.SimulateAsync(paymentId, ProviderOperationStatus.Succeeded, default);
            });
        }
        await using (var read = fixture.Context())
        {
            Assert.Equal(before, await PaymentPgCountsAsync(read));
            var payment = await read.Payments.SingleAsync();
            Assert.Null(payment.PaidAt);
            Assert.Equal(ProviderOperationStatus.Pending, payment.ProviderStatus);
            Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending), payment.PaymentStatusId);
            Assert.Empty(await read.Set<ReservationStatusHistory>().ToListAsync());
        }
        await using var retry = fixture.Context();
        if (bill) await PaymentPgScope(retry).Bill.SimulateAsync(1001, paymentId, ProviderOperationStatus.Succeeded, default);
        else await PaymentPgScope(retry).Provider.SimulateAsync(paymentId, ProviderOperationStatus.Succeeded, default);
        Assert.NotNull((await retry.Payments.SingleAsync()).PaidAt);
    }

    [StockPostgresFact]
    public async Task PaymentPgRefundSuccessRollbackPreservesPaidMoneyAndProcessingRefund()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        await PaymentPgPaidCancellationAsync(fixture);
        var refundId = await PaymentPgRefundAsync(fixture);
        PaymentPgCounts before;
        await using (var read = fixture.Context()) before = await PaymentPgCountsAsync(read);
        await using (var request = fixture.Context(new PaymentPgSaveFailure(notification: true)))
            await Assert.ThrowsAsync<DbUpdateException>(() => PaymentPgScope(request).Provider.SimulateRefundAsync(refundId, ProviderOperationStatus.Succeeded, default));
        await using (var read = fixture.Context())
        {
            Assert.Equal(before, await PaymentPgCountsAsync(read));
            var payment = await read.Payments.SingleAsync();
            var refund = await read.ReservationRefunds.SingleAsync();
            Assert.Null(payment.RefundedAt);
            Assert.Null(refund.RefundedAt);
            Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid), payment.PaymentStatusId);
            Assert.Equal(StatusIds.Refund(RefundStatus.Processing), refund.StatusId);
            Assert.Equal(ProviderOperationStatus.Pending, refund.ProviderStatus);
        }
        await using var retry = fixture.Context();
        await PaymentPgScope(retry).Provider.SimulateRefundAsync(refundId, ProviderOperationStatus.Succeeded, default);
        Assert.NotNull((await retry.ReservationRefunds.SingleAsync()).RefundedAt);
        Assert.Equal(1, await PaymentPgAuditCountAsync(retry, AuditActions.ProviderRefundUpdated));
    }

    [StockPostgresFact]
    public async Task PaymentPgRollbackReleasesRealUserAndTableLocksForWaitingSameKeyRequest()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var gate = new PaymentPgGate();
        var waiting = new PaymentPgLockSignal(userLock: true);
        var key = Guid.NewGuid().ToString("D");
        await using var firstDb = fixture.Context();
        await using var secondDb = fixture.Context(waiting);
        var first = Task.Run(() => AttemptAsync(async () =>
        {
            await PaymentPgScope(firstDb, gateway: new PaymentPgGateway("checkout", gate)).Provider.CreateCheckoutAsync(1001, key, default);
        }));
        Task<CheckoutPaymentResponse>? second = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            second = Task.Run(() => PaymentPgScope(secondDb).Provider.CreateCheckoutAsync(1001, key, default));
            await waiting.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(second.IsCompleted);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.IsType<IOException>(await first.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.NotNull(second);
        var result = await second!.WaitAsync(TimeSpan.FromSeconds(20));
        await using var read = fixture.Context();
        Assert.Equal(result.Id, Assert.Single(await read.Payments.ToListAsync()).Id);
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderCheckoutCreated));
    }

    [StockPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPgLiveAccountOrAssignmentIsRecheckedAfterRealTableLock(bool staff)
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill: true);
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await new TableRepository(owner).AcquireReservationLockAsync(1001, 1001);
        var signal = new PaymentPgLockSignal();
        await using var blockedDb = fixture.Context(signal);
        var request = Task.Run(() => AttemptAsync(async () =>
        {
            await PaymentPgScope(blockedDb, staff ? 1004 : 1001).Bill.CheckoutAsync(1001, Guid.NewGuid().ToString("D"), default);
        }));
        try
        {
            await signal.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(request.IsCompleted);
            await using var edit = fixture.Context();
            if (staff) (await edit.UserRestaurants.SingleAsync(value => value.UserId == 1004)).IsActive = false;
            else (await edit.Users.SingleAsync(value => value.Id == 1001)).IsActive = false;
            await edit.SaveChangesAsync();
        }
        finally { await transaction.RollbackAsync(); }
        var error = await request.WaitAsync(TimeSpan.FromSeconds(20));
        if (staff) Assert.IsType<NotFoundException>(error);
        else Assert.IsType<ForbiddenException>(error);
        await using var read = fixture.Context();
        Assert.Empty(await read.Payments.ToListAsync());
        Assert.Empty(await read.OutboxEvents.ToListAsync());
    }

    [StockPostgresFact]
    public async Task PaymentPgOtherCustomerOrRestaurantAndNonTestPaymentRemainProtected()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var paymentId = await PaymentPgCheckoutAsync(fixture);
        await using (var db = fixture.Context())
        {
            await Assert.ThrowsAsync<NotFoundException>(() => PaymentPgScope(db, 1002).Provider.GetReservationAsync(1001, default));
            await Assert.ThrowsAsync<NotFoundException>(() => PaymentPgScope(db, 1002).Provider.SimulateAsync(paymentId, ProviderOperationStatus.Succeeded, default));
            await Assert.ThrowsAsync<ForbiddenException>(() => PaymentPgScope(db, 1005, 1002).Provider.AuthorizeDepositAsync(1001, 1001, default));
            (await db.Payments.SingleAsync()).IsTest = false;
            await db.SaveChangesAsync();
        }
        await using var request = fixture.Context();
        await Assert.ThrowsAsync<BusinessRuleException>(() => PaymentPgScope(request).Provider.SimulateAsync(paymentId, ProviderOperationStatus.Succeeded, default));
        Assert.Null((await request.Payments.SingleAsync()).PaidAt);
    }

    [StockPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPgAbortedCheckoutRollsBackAlreadyInsertedPaymentAndReleasesKey(bool bill)
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill);
        var key = Guid.NewGuid().ToString("D");
        using var cancelled = new CancellationTokenSource();
        await using (var db = fixture.Context())
        {
            var services = PaymentPgScope(db, gateway: new PaymentPgGateway(cancel: cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                if (bill) await services.Bill.CheckoutAsync(1001, key, cancelled.Token);
                else await services.Provider.CreateCheckoutAsync(1001, key, cancelled.Token);
            });
        }
        await using (var read = fixture.Context())
        {
            Assert.Empty(await read.Payments.ToListAsync());
            Assert.Empty(await read.OutboxEvents.ToListAsync());
        }
        await using var retry = fixture.Context();
        if (bill) await PaymentPgScope(retry).Bill.CheckoutAsync(1001, key, default);
        else await PaymentPgScope(retry).Provider.CreateCheckoutAsync(1001, key, default);
        Assert.Single(await retry.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task PaymentPgLateParallelSuccessCreatesOneCompensationWithoutRevivingReservation()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var id = await PaymentPgCheckoutAsync(fixture);
        await using (var setup = fixture.Context())
        {
            var reservation = await setup.Reservations.SingleAsync();
            reservation.StatusId = StatusIds.Reservation(ReservationStatus.Expired);
            reservation.HoldExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await setup.SaveChangesAsync();
        }
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = fixture.Context();
            await PaymentPgScope(db).Provider.SimulateAsync(id, ProviderOperationStatus.Succeeded, default);
        })).WaitAsync(TimeSpan.FromSeconds(30));
        await using var read = fixture.Context();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Expired), (await read.Reservations.SingleAsync()).StatusId);
        Assert.True((await read.Payments.SingleAsync()).RequiresRefund);
        Assert.Single(await read.ReservationRefunds.ToListAsync());
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ReservationRefundRequested));
        Assert.Equal(1, await PaymentPgAuditCountAsync(read, AuditActions.ProviderPaymentUpdated));
    }

    [StockPostgresFact]
    public async Task PaymentPgFailedLateCompensationRollsBackPaymentCaptureAndAllRefundEffects()
    {
        await using var fixture = await PaymentPgDatabaseAsync();
        var id = await PaymentPgCheckoutAsync(fixture);
        PaymentPgCounts before;
        await using (var setup = fixture.Context())
        {
            (await setup.Reservations.SingleAsync()).StatusId = StatusIds.Reservation(ReservationStatus.Expired);
            await setup.SaveChangesAsync();
            before = await PaymentPgCountsAsync(setup);
        }
        await using (var request = fixture.Context())
            await Assert.ThrowsAsync<IOException>(() => PaymentPgScope(request, gateway: new PaymentPgGateway("refund"))
                .Provider.SimulateAsync(id, ProviderOperationStatus.Succeeded, default));
        await using (var read = fixture.Context())
        {
            Assert.Equal(before, await PaymentPgCountsAsync(read));
            var payment = await read.Payments.SingleAsync();
            Assert.Null(payment.PaidAt);
            Assert.False(payment.RequiresRefund);
            Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending), payment.PaymentStatusId);
        }
        await using var retry = fixture.Context();
        await PaymentPgScope(retry).Provider.SimulateAsync(id, ProviderOperationStatus.Succeeded, default);
        Assert.Single(await retry.ReservationRefunds.ToListAsync());
    }

    [StockPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentPgDatabaseUniqueIndexAlsoRejectsDuplicatePendingPayment(bool bill)
    {
        await using var fixture = await PaymentPgDatabaseAsync(bill);
        var id = await PaymentPgCheckoutAsync(fixture, bill);
        await using (var request = fixture.Context())
        {
            var source = await request.Payments.AsNoTracking().SingleAsync();
            request.Payments.Add(new Payment { RestaurantId = source.RestaurantId, ReservationId = source.ReservationId,
                TableSessionId = source.TableSessionId, BillFingerprint = source.BillFingerprint, CreatedByUserId = source.CreatedByUserId,
                Amount = source.Amount, CurrencyCode = source.CurrencyCode, CheckoutKey = Guid.NewGuid().ToString("D"),
                ProviderName = "simulation", ProviderRef = Guid.NewGuid().ToString("N"), IsTest = true,
                PaymentMethodId = source.PaymentMethodId, PaymentStatusId = source.PaymentStatusId });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => request.SaveChangesAsync());
            var postgres = Assert.IsType<Npgsql.PostgresException>(error.InnerException);
            Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal(bill ? "payments_pending_session_key" : "payments_pending_reservation_key", postgres.ConstraintName);
        }
        await using var read = fixture.Context();
        Assert.Equal(id, Assert.Single(await read.Payments.ToListAsync()).Id);
    }

    /// <summary>Mövcud təhlükəsiz GUID-li lokal PostgreSQL fixture-inə yalnız sintetik maliyyə məlumatlarını əlavə edir.</summary>
    private static async Task<Database> PaymentPgDatabaseAsync(bool bill = false)
    {
        var fixture = await Database.CreateAsync();
        try
        {
            await using var db = fixture.Context();
            db.Restaurants.Add(new Restaurant { Id = 1002, Name = "Other payment test", Location = "Baku", Phone = "1002", IsActive = true });
            foreach (var id in new[] { 1002, 1003, 1004, 1005 })
                db.Users.Add(new User { Id = id, Name = "Payment", Surname = "Test", Email = $"payment{id}@example.test", Phone = id.ToString(),
                    Password = "test-only", RoleId = id is 1003 or 1005 ? (int)RoleCode.Manager : id == 1004 ? (int)RoleCode.Waiter : (int)RoleCode.Customer, IsActive = true });
            db.Tables.AddRange(new Table { Id = 1001, RestaurantId = 1001, TableNo = 1, Capacity = 4, IsActive = true },
                new Table { Id = 1002, RestaurantId = 1002, TableNo = 1, Capacity = 4, IsActive = true });
            db.UserRestaurants.AddRange(new UserRestaurant { UserId = 1003, RestaurantId = 1001, RoleId = (int)RoleCode.Manager, IsActive = true },
                new UserRestaurant { UserId = 1004, RestaurantId = 1001, RoleId = (int)RoleCode.Waiter, IsActive = true },
                new UserRestaurant { UserId = 1005, RestaurantId = 1002, RoleId = (int)RoleCode.Manager, IsActive = true });
            var reservation = PaymentPgReservation(1001, 1001, 1001);
            if (bill)
            {
                reservation.StatusId = StatusIds.Reservation(ReservationStatus.Seated);
                reservation.DepositAmount = 0;
                reservation.ReservedAt = DateTime.UtcNow.AddHours(-1);
                db.Categories.Add(new Category { Id = 1001, RestaurantId = 1001, Name = "Test", Slug = "payment-test", IsActive = true });
                db.Items.Add(new Item { Id = 1001, RestaurantId = 1001, CategoryId = 1001, Name = "Tea", BasePrice = 7,
                    StatusId = StatusIds.Build(StatusType.ItemStatus, (int)ItemStatus.Available), IsActive = true, IsAvailable = true });
                db.TableSessions.Add(new TableSession { Id = 1001, RestaurantId = 1001, TableId = 1001, ReservationId = 1001,
                    CustomerUserId = 1001, WaiterUserId = 1004, OpenedAt = DateTime.UtcNow, StatusId = StatusIds.TableSession(TableSessionStatus.Open) });
                db.Orders.Add(new Order { Id = 1001, RestaurantId = 1001, TableId = 1001, TableSessionId = 1001,
                    ReservationId = 1001, CustomerUserId = 1001, WaiterUserId = 1004, SourceId = (int)OrderSource.Waiter,
                    StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served), ServedAt = DateTime.UtcNow,
                    OrderItems = [new OrderItem { Id = 1001, ItemId = 1001, NameSnapshot = "Tea", Quantity = 1, UnitPrice = 7, UnitPriceSnapshot = 7, LineTotal = 7 }] });
            }
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    /// <summary>Restoranın icazə verdiyi və müddəti bitməmiş 20 AZN depozit precondition-u yaradır.</summary>
    private static Reservation PaymentPgReservation(int id, int restaurant, int table) => new()
    {
        Id = id, RestaurantId = restaurant, TableId = table, CustomerUserId = 1001, PeopleCount = 2, DepositAmount = 20,
        ReservedAt = DateTime.UtcNow.AddHours(3), HoldExpiresAt = DateTime.UtcNow.AddHours(1),
        StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment)
    };

    /// <summary>Həqiqi repository, SQL kilidi, transaction, audit və bildiriş/push/e-mail outbox-larını eyni DB kontekstində birləşdirir.</summary>
    private static (ProviderPaymentManager Provider, TableSessionBillingManager Bill) PaymentPgScope(ECafeDbContext db,
        int userId = 1001, int restaurantId = 1001, IPaymentGateway? gateway = null)
    {
        var role = userId is 1003 or 1005 ? RoleCode.Manager : userId == 1004 ? RoleCode.Waiter : RoleCode.Customer;
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", userId.ToString()), new Claim(ClaimTypes.Role, ((int)role).ToString()), new Claim("restaurantId", restaurantId.ToString())], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(value => value.HttpContext == context);
        var mapper = new MapperConfiguration(options => options.AddProfile<NotificationProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["MobileApp:PushDeliveryReady"] = "true", ["MobileApp:ExpoProjectId"] = "071e8108-0b20-4b56-87d2-6c177f953eaa" }).Build();
        var dispatcher = Mock.Of<INotificationChangeDispatcher>();
        var assignments = new UserRestaurantRepository(db);
        var notices = new NotificationManager(accessor, mapper, config, new NotificationRepository(db), new CreateNotificationRequestValidator(),
            dispatcher, new MobilePushOutboxWriter(db, config), new NotificationEmailOutboxWriter(db), assignments);
        var audit = new AuditLogManager(accessor, mapper, config, new BaseRepository<AuditLog>(db), new BaseRepository<OutboxEvent>(db));
        var transactions = new PaymentPgTransactions(db, dispatcher);
        var runtime = new PaymentRuntime(Options.Create(new PaymentOptions()), new("Testing"));
        gateway ??= new SimulationPaymentGateway();
        return (new(accessor, mapper, config, new ReservationRepository(db), new BaseRepository<Payment>(db), new ReservationRefundRepository(db),
            new TableRepository(db), new UserRepository(db), assignments, transactions, Mock.Of<IWorkflowActionService>(), gateway, runtime,
            Options.Create(new ReservationTimingOptions()), TimeProvider.System, notices, new ReservationRefundNotifier(assignments, notices),
            audit, Mock.Of<IFileAccessUrlService>()),
            new(accessor, mapper, config, new TableSessionRepository(db), new BaseRepository<Payment>(db), new TableRepository(db),
                new UserRepository(db), assignments, transactions, gateway, runtime, TimeProvider.System, audit, notices));
    }

    /// <summary>Test əməliyyatını ayrı real bağlantıda yaradır və sonrakı yarışa yalnız ID ötürür.</summary>
    private static async Task<int> PaymentPgCheckoutAsync(Database fixture, bool bill = false)
    {
        await using var db = fixture.Context();
        var services = PaymentPgScope(db);
        var key = Guid.NewGuid().ToString("D");
        return bill ? (await services.Bill.CheckoutAsync(1001, key, default)).Id
            : (await services.Provider.CreateCheckoutAsync(1001, key, default)).Id;
    }

    /// <summary>Refund hüququnu ödənişə bağlayan sintetik ödənmiş/ləğv edilmiş rezervasiya qurur.</summary>
    private static async Task<int> PaymentPgPaidCancellationAsync(Database fixture)
    {
        var id = await PaymentPgCheckoutAsync(fixture);
        await using var db = fixture.Context();
        await PaymentPgScope(db).Provider.SimulateAsync(id, ProviderOperationStatus.Succeeded, default);
        var reservation = await db.Reservations.SingleAsync();
        reservation.StatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
        reservation.RefundEligible = true;
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Refund yaranmasını ayrıca transaction-da təsdiqləyir; nəticə testləri təsdiqlənməmiş pulu geri qaytarılmış saymır.</summary>
    private static async Task<int> PaymentPgRefundAsync(Database fixture)
    {
        await using var db = fixture.Context();
        return (await PaymentPgScope(db).Provider.RequestRefundAsync(1001, default)).Id;
    }

    /// <summary>Audit outbox payload-unu strukturla oxuyaraq təkrar yan təsirin sayını yoxlayır.</summary>
    private static async Task<int> PaymentPgAuditCountAsync(ECafeDbContext db, string action)
    {
        var payloads = await db.OutboxEvents.Where(value => value.EventType == OutboxEventTypes.AuditLogRequested).Select(value => value.Payload).ToListAsync();
        return payloads.Count(value => PaymentPgIsAuditAction(value, action));
    }

    /// <summary>Audit JSON sənədini istifadə bitəndə bağlayaraq action sahəsini dəqiq müqayisə edir.</summary>
    private static bool PaymentPgIsAuditAction(string payload, string action)
    { using var json = JsonDocument.Parse(payload); return json.RootElement.GetProperty("action").GetString() == action; }

    /// <summary>Rollback-dən əvvəl və sonra maliyyə, tarixçə və outbox saylarını eyni snapshot kimi müqayisə edir.</summary>
    private static async Task<PaymentPgCounts> PaymentPgCountsAsync(ECafeDbContext db) => new(await db.Payments.CountAsync(),
        await db.ReservationRefunds.CountAsync(), await db.Set<ReservationRefundStatusHistory>().CountAsync(),
        await db.Set<ReservationStatusHistory>().CountAsync(), await db.Notifications.CountAsync(), await db.OutboxEvents.CountAsync());

    private sealed record PaymentPgCounts(int Payments, int Refunds, int RefundHistory, int ReservationHistory, int Notifications, int Outbox);

    private sealed class PaymentPgTransactions(ECafeDbContext db, INotificationChangeDispatcher dispatcher) : IApplicationDbTransactionFactory
    {
        /// <summary>Mock transaction deyil, production commit/dispose wrapper-i ilə həqiqi PostgreSQL transaction açır.</summary>
        public async Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => new EfApplicationDbTransaction(await db.Database.BeginTransactionAsync(cancellationToken), dispatcher);
        /// <summary>Xidmətin istədiyi ReadCommitted izolyasiyasını eyni real bağlantıda saxlayır.</summary>
        public async Task<IApplicationDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
            => new EfApplicationDbTransaction(await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken), dispatcher);
    }

    private sealed class PaymentPgGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PaymentPgGateway(string? failure = null, PaymentPgGate? gate = null, CancellationTokenSource? cancel = null) : IPaymentGateway
    {
        private readonly SimulationPaymentGateway inner = new();
        public string Name => inner.Name;
        public bool IsTest => true;
        /// <summary>Yalnız lokal simulyasiya edir; DB yazısından sonra deterministik xəta və kilid gözləməsi yaradır.</summary>
        public async Task CreateCheckoutAsync(Payment payment, CancellationToken cancellationToken)
        {
            await inner.CreateCheckoutAsync(payment, cancellationToken);
            cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (gate != null) { gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); }
            if (failure == "checkout") throw new IOException("Synthetic checkout failure after payment insert.");
        }
        /// <summary>Heç bir bank çağırmadan saxlanmış test ödəniş nəticəsini qaytarır.</summary>
        public Task<GatewayPaymentResult> VerifyPaymentAsync(Payment payment, CancellationToken cancellationToken)
            => inner.VerifyPaymentAsync(payment, cancellationToken);
        /// <summary>Refund və ilkin tarixçə yazısından sonra xəta ataraq real rollback-i yoxlayır.</summary>
        public async Task CreateRefundAsync(Payment payment, ReservationRefund refund, CancellationToken cancellationToken)
        {
            await inner.CreateRefundAsync(payment, refund, cancellationToken);
            if (failure == "refund") throw new IOException("Synthetic refund failure after refund insert.");
        }
        /// <summary>Yalnız test refundunu oxuyur; xarici şəbəkəyə sorğu göndərmir.</summary>
        public Task<GatewayPaymentResult> VerifyRefundAsync(Payment payment, ReservationRefund refund, CancellationToken cancellationToken)
            => inner.VerifyRefundAsync(payment, refund, cancellationToken);
    }

    private sealed class PaymentPgSaveFailure(string? action = null, bool notification = false) : SaveChangesInterceptor
    {
        private bool fired;
        /// <summary>SQL yazıları həqiqətən bitdikdən sonra xəta yaradır; bütün əvvəlki maliyyə/outbox yazılarının geri alınmasını tələb edir.</summary>
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var db = (ECafeDbContext)eventData.Context!;
            if (!fired && (notification && db.Notifications.Local.Any() || action != null && db.OutboxEvents.Local.Any(value =>
                    value.EventType == OutboxEventTypes.AuditLogRequested && PaymentPgIsAuditAction(value.Payload, action))))
            { fired = true; throw new DbUpdateException("Synthetic failure after SQL writes, before commit."); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PaymentPgLockSignal(bool userLock = false) : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Həqiqi SQL əmri icra olunmağa başlayanda siqnal verir; kilidi mock və ya SemaphoreSlim ilə əvəz etmir.</summary>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (userLock ? command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) :
                command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) && command.Parameters.Count == 2 &&
                command.Parameters[1].Value is int table && table == 1001) Entered.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
