using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Repositories.TableSession;
using Microsoft.EntityFrameworkCore;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed partial class TableSessionCloseReviewTests
{
    [Theory]
    [InlineData(RoleCode.Waiter, 1)]
    [InlineData(RoleCode.Manager, 3)]
    [InlineData(RoleCode.Owner, 4)]
    public async Task SevenAznCoveredByConfirmedDepositClosesOnceWithoutAnAdditionalPayment(RoleCode role, int actor)
    {
        await using var f = await Fixture.CreateAsync();
        await SeedSevenAznBillAsync(f, 7, true);
        f.Actor(actor, role);
        var bill = await ReadCompletionBillAsync(f);
        Assert.Equal(7m, bill.TotalAmount);
        Assert.Equal(7m, bill.DepositCreditAmount);
        Assert.Equal(0m, bill.PaidAmount);
        Assert.Equal(0m, bill.RemainingAmount);
        Assert.False(bill.HasUnservedOrders);

        await CompleteForActorAsync(f, role);
        var closedAt = (await f.Db.TableSessions.SingleAsync(session => session.Id == 10)).ClosedAt;
        Assert.NotNull(closedAt);
        await CompleteForActorAsync(f, role);
        Assert.Equal(closedAt, (await f.Db.TableSessions.SingleAsync(session => session.Id == 10)).ClosedAt);
        Assert.Single(await f.Db.Payments.ToListAsync());
        Assert.Empty(await f.Db.Payments.Where(payment => payment.TableSessionId == 10).ToListAsync());
        Assert.Single((await f.Db.Reservations.Include(reservation => reservation.StatusHistory).SingleAsync()).StatusHistory);
        Assert.Single(f.Sent, notification => notification.TypeId == (int)NotificationType.ReservationCompleted);
    }

    [Theory]
    [InlineData(RoleCode.Waiter, 1, 0, false, false, 7)]
    [InlineData(RoleCode.Manager, 3, 0, false, false, 7)]
    [InlineData(RoleCode.Owner, 4, 0, false, false, 7)]
    [InlineData(RoleCode.Waiter, 1, 3, true, false, 4)]
    [InlineData(RoleCode.Manager, 3, 3, true, false, 4)]
    [InlineData(RoleCode.Owner, 4, 3, true, false, 4)]
    [InlineData(RoleCode.Waiter, 1, 7, false, false, 7)]
    [InlineData(RoleCode.Manager, 3, 7, false, false, 7)]
    [InlineData(RoleCode.Owner, 4, 7, false, false, 7)]
    [InlineData(RoleCode.Waiter, 1, 7, true, true, 7)]
    [InlineData(RoleCode.Manager, 3, 7, true, true, 7)]
    [InlineData(RoleCode.Owner, 4, 7, true, true, 7)]
    public async Task EveryCompletionPathRejectsRealDebtWithoutChangingReservationOrPayments(
        RoleCode role, int actor, int deposit, bool confirmed, bool refunded, int remaining)
    {
        await using var f = await Fixture.CreateAsync();
        await SeedSevenAznBillAsync(f, deposit, confirmed, refunded);
        f.Actor(actor, role);
        var before = await ReadCompletionBillAsync(f);
        Assert.Equal(7m, before.TotalAmount);
        Assert.Equal((decimal)remaining, before.RemainingAmount);
        var payments = await f.Db.Payments.CountAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteForActorAsync(f, role));
        Assert.Equal(ErrorCode.SessionBillUnpaid, error.Code);
        Assert.Null((await f.Db.TableSessions.SingleAsync(session => session.Id == 10)).ClosedAt);
        var reservation = await f.Db.Reservations.Include(item => item.StatusHistory).SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Seated), reservation.StatusId);
        Assert.Null(reservation.CompletedAt);
        Assert.Empty(reservation.StatusHistory);
        Assert.Equal(payments, await f.Db.Payments.CountAsync());
        Assert.DoesNotContain(f.Sent, notification => notification.TypeId == (int)NotificationType.ReservationCompleted);
    }

    [Theory]
    [InlineData(RoleCode.Waiter, 1)]
    [InlineData(RoleCode.Manager, 3)]
    [InlineData(RoleCode.Owner, 4)]
    public async Task CompletionRechecksNewDebtAfterTheTableLockInsteadOfTrustingAnEarlierZeroBill(RoleCode role, int actor)
    {
        await using var f = await Fixture.CreateAsync();
        await SeedSevenAznBillAsync(f, 0, false);
        f.Db.Payments.Add(new Payment
        {
            Id = 100, RestaurantId = 1, TableSessionId = 10, Amount = 7, CurrencyCode = "AZN",
            ProviderName = "simulation", ProviderRef = "settled-seven", IsTest = true, PaidAt = DateTime.UtcNow,
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid)
        });
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        f.Actor(actor, role);
        Assert.Equal(0m, (await ReadCompletionBillAsync(f)).RemainingAmount);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.BeforeLock = () => { waiting.TrySetResult(); return release.Task; };
        var completion = CompleteForActorAsync(f, role);
        try
        {
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var other = f.Fork();
            other.Db.Orders.Add(CompletionOrder(102, OrderStatus.Served, 7));
            await other.Db.SaveChangesAsync();
        }
        finally { release.TrySetResult(); }
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => completion);
        Assert.Equal(ErrorCode.SessionBillUnpaid, error.Code);
        var bill = await ReadCompletionBillAsync(f);
        Assert.Equal(14m, bill.TotalAmount);
        Assert.Equal(7m, bill.PaidAmount);
        Assert.Equal(7m, bill.RemainingAmount);
        Assert.Null((await f.Db.TableSessions.SingleAsync(session => session.Id == 10)).ClosedAt);
        Assert.DoesNotContain(f.Sent, notification => notification.TypeId == (int)NotificationType.ReservationCompleted);
    }

    /// <summary>Videodaki 7 AZN təqdim edilmiş və iki ləğv edilmiş sifarişi, ayrıca təsdiqlənən depozit ilə sorğulararası vəziyyət kimi saxlayır.</summary>
    private static async Task SeedSevenAznBillAsync(Fixture fixture, int deposit, bool confirmed, bool refunded = false)
    {
        (await fixture.Db.Reservations.SingleAsync()).DepositAmount = deposit;
        fixture.Db.Orders.AddRange(CompletionOrder(99, OrderStatus.Served, 7),
            CompletionOrder(100, OrderStatus.Cancelled, 12), CompletionOrder(101, OrderStatus.Cancelled, 18));
        if (deposit > 0) fixture.Db.Payments.Add(new Payment
        {
            Id = 99, RestaurantId = 1, ReservationId = 75, Amount = deposit, CurrencyCode = "AZN",
            ProviderName = "simulation", ProviderRef = "reservation-deposit", IsTest = true,
            PaidAt = confirmed ? DateTime.UtcNow : null, RefundedAt = refunded ? DateTime.UtcNow : null,
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)(refunded
                ? PaymentStatus.Refunded : confirmed ? PaymentStatus.Paid : PaymentStatus.Pending))
        });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
    }

    /// <summary>Saxlanmış qiymət və sessiya aidiyyəti ilə təqdim edilmiş və ya ləğv edilmiş sətir yaradır.</summary>
    private static Order CompletionOrder(int id, OrderStatus status, decimal amount) => new()
    {
        Id = id, RestaurantId = 1, TableId = 1, TableSessionId = 10, ReservationId = 75,
        StatusId = StatusIds.Build(StatusType.Order, (int)status),
        ServedAt = status == OrderStatus.Served ? DateTime.UtcNow : null,
        OrderItems = [new OrderItem { Id = id, ItemId = 1, NameSnapshot = "Americano", Quantity = 1,
            UnitPriceSnapshot = amount, UnitPrice = amount, LineTotal = amount }]
    };

    /// <summary>Frontendin oxuduğu hesab kimi tam qrafı tracking-siz yükləyir; əvvəlki context naviqasiyasına arxalanmır.</summary>
    private static async Task<TableSessionBill> ReadCompletionBillAsync(Fixture fixture)
        => TableSessionBillingPolicy.Calculate((await new TableSessionRepository(fixture.Db).GetForBillingAsync(10, false, default))!);

    /// <summary>Ofisiant və menecer/sahibkarın ayrı giriş yollarını eyni maliyyə ssenarisi ilə yoxlayır.</summary>
    private static Task CompleteForActorAsync(Fixture fixture, RoleCode role) => role == RoleCode.Waiter
        ? fixture.Reservations.CompleteOwnReservationAsync(1, 75)
        : fixture.Reservations.CompleteReservationAsync(1, 75);
}
