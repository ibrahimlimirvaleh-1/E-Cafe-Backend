using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Payments;
using ECafe.Application.Services.Payments.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed class TableSessionBillingTests
{
    [Fact]
    public async Task ConcurrentSameKeyUnderSharedTableLockCreatesOnePayment()
    {
        using var gate = new SemaphoreSlim(1, 1);
        await using var first = await Fixture.CreateAsync(gate);
        await using var second = first.Fork(gate);
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(first.Service.CheckoutAsync(1, key, default), second.Service.CheckoutAsync(1, key, default));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(first.Db.Payments);
    }

    [Fact]
    public async Task ConcurrentDifferentActorsCannotOpenTwoPendingCheckouts()
    {
        using var gate = new SemaphoreSlim(1, 1);
        await using var first = await Fixture.CreateAsync(gate);
        await using var second = first.Fork(gate);
        second.Actor(2, RoleCode.Waiter);
        var outcomes = await Task.WhenAll(Record.ExceptionAsync(() => first.Checkout()), Record.ExceptionAsync(() => second.Checkout()));
        Assert.Single(outcomes, outcome => outcome == null);
        Assert.Single(outcomes, outcome => outcome is BusinessRuleException);
        Assert.Single(first.Db.Payments);
    }

    [Fact]
    public async Task UnpaidBillUsesStoredSnapshotsAndDoesNotTreatServedAsPaid()
    {
        await using var f = await Fixture.CreateAsync();
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(50, bill.TotalAmount);
        Assert.Equal(50, bill.RemainingAmount);
        Assert.Equal(0, bill.PaidAmount);
        Assert.True(bill.CanPay);
        Assert.False(bill.CanClose);
        Assert.Throws<BusinessRuleException>(() => TableSessionBillingPolicy.EnsureCanClose(f.Session));
        Assert.Empty(f.Db.Payments);
    }

    [Fact]
    public async Task SameKeyReplaysAcrossRefreshWithoutAnotherPaymentOrAudit()
    {
        await using var f = await Fixture.CreateAsync();
        var key = Guid.NewGuid().ToString();
        var first = await f.Service.CheckoutAsync(1, key, default);
        var replay = await f.Service.CheckoutAsync(1, key, default);
        Assert.Equal(first, replay);
        Assert.Equal(50, first.Amount);
        Assert.True(first.IsTest);
        Assert.Null(first.CheckoutUrl);
        Assert.Single(f.Db.Payments);
        f.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.SessionBillCheckoutCreated,
            It.IsAny<object>(), AuditEntityTypes.TableSession, 1, null), Times.Once);
        Assert.False((await f.Service.GetAsync(1, default)).CanPay);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        f.Actor(2, RoleCode.Waiter);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
    }

    [Fact]
    public async Task SuccessReconcilesExactlyOnceAndEnablesStaffCloseButNotCustomerClose()
    {
        await using var f = await Fixture.CreateAsync();
        var payment = await f.Checkout();
        Assert.Equal("Pending", (await f.Service.RefreshAsync(1, payment.Id, default)).Status);
        var paid = await f.Service.SimulateAsync(1, payment.Id, ProviderOperationStatus.Succeeded, default);
        Assert.NotNull(paid.PaidAt);
        Assert.Equal(paid, await f.Service.SimulateAsync(1, payment.Id, ProviderOperationStatus.Succeeded, default));
        Assert.Equal(paid, await f.Service.RefreshAsync(1, payment.Id, default));
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(0, bill.RemainingAmount);
        Assert.False(bill.CanClose);
        f.Actor(2, RoleCode.Waiter);
        Assert.True((await f.Service.GetAsync(1, default)).CanClose);
        TableSessionBillingPolicy.EnsureCanClose(f.Session);
        Assert.Null(f.Session.ClosedAt);
        f.Audit.Verify(a => a.RecordRestaurantActionAsync(1, AuditActions.SessionBillPaymentUpdated,
            It.IsAny<object>(), AuditEntityTypes.TableSession, 1, null), Times.Once);
        f.Notifications.Verify(n => n.CreateAsync(It.Is<CreateNotificationRequest>(r =>
            r.UserId == 1 && r.TypeId == (int)NotificationType.TableSessionPaid)), Times.Once);
        f.Notifications.Verify(n => n.CreateAsync(It.Is<CreateNotificationRequest>(r => r.UserId == 2)), Times.Once);
        var saved = await f.Db.Payments.SingleAsync();
        Assert.Null(saved.ReservationId);
        Assert.Null(saved.OrderId);
        Assert.Equal(1, saved.TableSessionId);
    }

    [Theory]
    [InlineData(ProviderOperationStatus.Failed)]
    [InlineData(ProviderOperationStatus.Cancelled)]
    public async Task FailureRetainsDebtAndNewAttemptUsesANewKey(ProviderOperationStatus outcome)
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.Checkout();
        await f.Service.SimulateAsync(1, first.Id, outcome, default);
        Assert.Equal(50, (await f.Service.GetAsync(1, default)).RemainingAmount);
        Assert.False((await f.Service.GetAsync(1, default)).CanClose);
        var next = await f.Checkout();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(2, await f.Db.Payments.CountAsync());
        await f.Service.SimulateAsync(1, first.Id, ProviderOperationStatus.Succeeded, default);
        Assert.Equal(50, (await f.Service.GetAsync(1, default)).RemainingAmount);
        await f.Service.SimulateAsync(1, next.Id, ProviderOperationStatus.Succeeded, default);
        Assert.Equal(0, (await f.Service.GetAsync(1, default)).RemainingAmount);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.Accepted)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    public async Task KitchenAndUnsentOrdersBlockCheckoutAndClosure(OrderStatus status)
    {
        await using var f = await Fixture.CreateAsync();
        f.Order.StatusId = StatusIds.Build(StatusType.Order, (int)status);
        await f.Db.SaveChangesAsync();
        var bill = await f.Service.GetAsync(1, default);
        Assert.True(bill.HasUnservedOrders);
        Assert.False(bill.CanPay);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        Assert.Throws<BusinessRuleException>(() => TableSessionBillingPolicy.EnsureCanClose(f.Session));
    }

    [Fact]
    public async Task AdditionalServedOrderAfterPaymentRequiresOnlyAdditionalAmount()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.Checkout();
        await f.Service.SimulateAsync(1, first.Id, ProviderOperationStatus.Succeeded, default);
        f.Db.Orders.Add(Fixture.NewOrder(2, 12));
        await f.Db.SaveChangesAsync();
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(62, bill.TotalAmount);
        Assert.Equal(50, bill.PaidAmount);
        Assert.Equal(12, bill.RemainingAmount);
        Assert.Throws<BusinessRuleException>(() => TableSessionBillingPolicy.EnsureCanClose(f.Session));
        var second = await f.Checkout();
        Assert.Equal(12, second.Amount);
        await f.Service.SimulateAsync(1, second.Id, ProviderOperationStatus.Succeeded, default);
        TableSessionBillingPolicy.EnsureCanClose(f.Session);
    }

    [Fact]
    public async Task DepositOffsetsOnceAndRefundedDepositCannotBeReused()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Reservations.Add(new Reservation { Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            StatusId = StatusIds.Reservation(ReservationStatus.Seated), DepositAmount = 10, PeopleCount = 2 });
        f.Session.ReservationId = 1;
        var deposit = new Payment { Id = 5, RestaurantId = 1, ReservationId = 1, Amount = 10, PaidAt = DateTime.UtcNow,
            ProviderName = "simulation", ProviderRef = "deposit", IsTest = true,
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) };
        f.Db.Payments.Add(deposit);
        await f.Db.SaveChangesAsync();
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(10, bill.DepositCreditAmount);
        Assert.Equal(40, (await f.Checkout()).Amount);
        deposit.RefundedAt = DateTime.UtcNow;
        deposit.PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded);
        await f.Db.SaveChangesAsync();
        Assert.Equal(0, (await f.Service.GetAsync(1, default)).DepositCreditAmount);
    }

    [Fact]
    public async Task ModifiedPendingBillCannotBeSilentlyPaidOrClosedOrChargedAgain()
    {
        await using var f = await Fixture.CreateAsync();
        var payment = await f.Checkout();
        f.Order.OrderItems.First().LineTotal = 60;
        f.Order.OrderItems.First().UnitPriceSnapshot = 30;
        await f.Db.SaveChangesAsync();
        var result = await f.Service.SimulateAsync(1, payment.Id, ProviderOperationStatus.Succeeded, default);
        Assert.True(result.RequiresRefund);
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(60, bill.RemainingAmount);
        Assert.False(bill.CanPay);
        Assert.False(bill.CanClose);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        Assert.Throws<BusinessRuleException>(() => TableSessionBillingPolicy.EnsureCanClose(f.Session));
    }

    [Fact]
    public async Task LegacyApprovedDepositIsHonouredWithoutAcceptingNewProofOrBankDetails()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Reservations.Add(new Reservation { Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            StatusId = StatusIds.Reservation(ReservationStatus.Seated), DepositAmount = 10, PeopleCount = 2 });
        f.Session.ReservationId = 1;
        f.Db.ReservationPaymentProofs.Add(new ReservationPaymentProof { Id = 1, ReservationId = 1, Amount = 10,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), ReviewedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        Assert.Equal(40, (await f.Checkout()).Amount);
        Assert.Single(f.Db.ReservationPaymentProofs);
    }

    [Fact]
    public async Task DepositCreditIsCappedAtTotalAndCannotBeConsumedBySecondSession()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Reservations.Add(new Reservation { Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            StatusId = StatusIds.Reservation(ReservationStatus.Seated), DepositAmount = 60, PeopleCount = 2 });
        f.Session.ReservationId = 1;
        f.Db.Payments.Add(new Payment { Id = 5, RestaurantId = 1, ReservationId = 1, Amount = 60, PaidAt = DateTime.UtcNow,
            ProviderName = "simulation", ProviderRef = "deposit", IsTest = true,
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) });
        await f.Db.SaveChangesAsync();
        Assert.Equal(50, (await f.Service.GetAsync(1, default)).DepositCreditAmount);
        Assert.Equal(0, (await f.Service.GetAsync(1, default)).RemainingAmount);
        f.Db.TableSessions.Add(new TableSession { Id = 2, RestaurantId = 1, TableId = 1, ReservationId = 1, CustomerUserId = 1 });
        await f.Db.SaveChangesAsync();
        var second = await new TableSessionRepository(f.Db).GetForBillingAsync(2, false, default);
        Assert.Equal(0, TableSessionBillingPolicy.Calculate(second!).DepositCreditAmount);
        Assert.Equal(60, (await f.Db.Payments.SingleAsync()).Amount);
        Assert.Empty(f.Db.ReservationRefunds);
    }

    [Fact]
    public async Task AccountDisabledAfterCheckoutCannotSimulatePayment()
    {
        await using var f = await Fixture.CreateAsync();
        var payment = await f.Checkout();
        (await f.Db.Users.SingleAsync(user => user.Id == 1)).IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SimulateAsync(1, payment.Id, ProviderOperationStatus.Succeeded, default));
        Assert.Null((await f.Db.Payments.SingleAsync()).PaidAt);
    }

    [Fact]
    public async Task DisabledRuntimeBlocksMutationsButKeepsBillReadable()
    {
        await using var f = await Fixture.CreateAsync();
        f.Options.EnableSimulation = false;
        var bill = await f.Service.GetAsync(1, default);
        Assert.False(bill.Enabled);
        Assert.False(bill.CanPay);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
    }

    [Theory]
    [InlineData(4, RoleCode.Customer)]
    [InlineData(4, RoleCode.Waiter)]
    [InlineData(4, RoleCode.Kitchen)]
    public async Task AnotherActorCannotReadPayOrReconcile(int id, RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        var payment = await f.Checkout();
        f.Actor(id, role);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.GetAsync(1, default));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Checkout());
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.SimulateAsync(1, payment.Id, ProviderOperationStatus.Succeeded, default));
        Assert.Null((await f.Db.Payments.SingleAsync()).PaidAt);
    }

    [Fact]
    public async Task StaffAssignmentRevokedDuringLockIsRejected()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(2, RoleCode.Waiter);
        f.BeforeLock = async () =>
        {
            (await f.Db.UserRestaurants.SingleAsync(a => a.UserId == 2)).IsActive = false;
            await f.Db.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<Exception>(() => f.Checkout());
        Assert.Empty(f.Db.Payments);
    }

    [Fact]
    public async Task ClosedSessionIsReadableButCannotStartPayment()
    {
        await using var f = await Fixture.CreateAsync();
        f.Session.StatusId = StatusIds.TableSession(TableSessionStatus.Closed);
        f.Session.ClosedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        Assert.False((await f.Service.GetAsync(1, default)).CanPay);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("invalid")]
    public async Task InvalidKeyCannotCreatePayment(string key)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.CheckoutAsync(1, key, default));
        Assert.Empty(f.Db.Payments);
    }

    [Fact]
    public async Task KeyCannotBeReusedForAnotherPaymentPurpose()
    {
        await using var f = await Fixture.CreateAsync();
        var key = Guid.NewGuid().ToString();
        f.Db.Payments.Add(new Payment { Id = 7, RestaurantId = 1, CreatedByUserId = 1, CheckoutKey = key,
            Amount = 10, PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) });
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CheckoutAsync(1, key, default));
        Assert.Single(f.Db.Payments);
    }

    [Fact]
    public async Task SoftDeletedPaymentKeepsItsIdempotencyKeyReserved()
    {
        await using var f = await Fixture.CreateAsync();
        var key = Guid.NewGuid().ToString();
        await f.Service.CheckoutAsync(1, key, default);
        (await f.Db.Payments.SingleAsync()).IsDeleted = true;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CheckoutAsync(1, key, default));
        Assert.Equal(ErrorCode.PaymentRequestKeyConflict, error.Code);
        Assert.Single(f.Db.Payments.IgnoreQueryFilters());
    }

    [Fact]
    public async Task NonTestPaymentAndMismatchedGatewayCannotBeSimulated()
    {
        await using var f = await Fixture.CreateAsync();
        var checkout = await f.Checkout();
        var payment = await f.Db.Payments.SingleAsync();
        payment.IsTest = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateAsync(1, checkout.Id, ProviderOperationStatus.Succeeded, default));
        payment.IsTest = true;
        payment.ProviderName = "different";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RefreshAsync(1, checkout.Id, default));
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task CancelledOrdersAreExcludedAndZeroBillNeedsNoFakePayment()
    {
        await using var f = await Fixture.CreateAsync();
        f.Order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        await f.Db.SaveChangesAsync();
        f.Actor(3, RoleCode.Manager);
        var bill = await f.Service.GetAsync(1, default);
        Assert.Equal(0, bill.RemainingAmount);
        Assert.True(bill.CanClose);
        Assert.False(bill.CanPay);
        TableSessionBillingPolicy.EnsureCanClose(f.Session);
        Assert.Empty(f.Db.Payments);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; }
        public DefaultHttpContext Http { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Func<Task>? BeforeLock { get; set; }
        public TableSessionBillingManager Service { get; }
        public PaymentOptions Options { get; } = new();
        public TableSession Session => Db.TableSessions.Local.Single();
        public Order Order => Db.Orders.Local.First();

        /// <summary>Həqiqi EF repository-ləri qurur; yalnız xarici audit, transaction və PostgreSQL kilid sərhədləri əvəzlənir.</summary>
        private Fixture(ECafeDbContext db, SemaphoreSlim? gate = null)
        {
            Db = db;
            Actor(1, RoleCode.Customer);
            var tables = new Mock<ITableRepository>();
            var locked = false;
            tables.Setup(t => t.AcquireReservationLockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    if (BeforeLock != null) await BeforeLock();
                    if (gate != null) { await gate.WaitAsync(); locked = true; }
                });
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return Task.CompletedTask;
            });
            transaction.Setup(t => t.DisposeAsync()).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return ValueTask.CompletedTask;
            });
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
            Service = new(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == Http), Mock.Of<IMapper>(), new ConfigurationBuilder().Build(),
                new TableSessionRepository(db), new BaseRepository<Payment>(db), tables.Object, new UserRepository(db),
                new UserRestaurantRepository(db), transactions.Object, new SimulationPaymentGateway(),
                new PaymentRuntime(Microsoft.Extensions.Options.Options.Create(Options), new("Testing")), TimeProvider.System, Audit.Object, Notifications.Object);
        }

        /// <summary>Bir restoran, öz və yad müştəri, məsul ofisiant və menecer ilə təqdim edilmiş 50 AZN hesabı hazırlayır.</summary>
        public static async Task<Fixture> CreateAsync(SemaphoreSlim? gate = null)
        {
            var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            await db.Database.EnsureCreatedAsync();
            db.Restaurants.Add(new Restaurant { Id = 1, Name = "Test", Location = "Baku", Phone = "1", TimeZone = "Asia/Baku", IsActive = true });
            db.Tables.Add(new Table { Id = 1, RestaurantId = 1, TableNo = 1, Capacity = 4, IsActive = true });
            for (var id = 1; id <= 4; id++) db.Users.Add(new User { Id = id, Name = "Test", Surname = id.ToString(), Email = $"{id}@test.example", Phone = id.ToString(), Password = "test", IsActive = true });
            db.UserRestaurants.AddRange(new UserRestaurant { Id = 1, RestaurantId = 1, UserId = 2, RoleId = (int)RoleCode.Waiter, IsActive = true },
                new UserRestaurant { Id = 2, RestaurantId = 1, UserId = 3, RoleId = (int)RoleCode.Manager, IsActive = true });
            db.TableSessions.Add(new TableSession { Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1, WaiterUserId = 2,
                StatusId = StatusIds.TableSession(TableSessionStatus.Open), OpenedAt = DateTime.UtcNow });
            db.Orders.Add(NewOrder(1, 50));
            await db.SaveChangesAsync();
            return new(db, gate);
        }

        /// <summary>Paralel sorğu üçün eyni yaddaş bazasında ayrı context və ortaq masa kilidi qurur; bu PostgreSQL inteqrasiya testi deyil.</summary>
        public Fixture Fork(SemaphoreSlim gate) => new(new ECafeDbContext((DbContextOptions<ECafeDbContext>)Db.GetService<IDbContextOptions>()), gate);

        /// <summary>Menyu dəyişikliklərindən asılı olmayan təqdim edilmiş sifariş snapshotu yaradır.</summary>
        public static Order NewOrder(int id, decimal total) => new()
        {
            Id = id, RestaurantId = 1, TableId = 1, TableSessionId = 1,
            StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served), ServedAt = DateTime.UtcNow,
            OrderItems = [new OrderItem { Id = id, ItemId = 1, Quantity = 2, UnitPriceSnapshot = total / 2,
                UnitPrice = total / 2, LineTotal = total, NameSnapshot = "Tea" }]
        };

        /// <summary>Sorğunun cari istifadəçi və restoran rolunu dəyişir; canlı təyinat ayrıca repository-də yoxlanır.</summary>
        public void Actor(int id, RoleCode role) => Http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", id.ToString()), new Claim(ClaimTypes.Role, ((int)role).ToString()), new Claim("restaurantId", "1")], "test"));
        /// <summary>Müstəqil ödəniş cəhdi üçün yeni açar yaradır.</summary>
        public Task<ECafe.Application.DTOs.Payment.SessionBillPaymentResponse> Checkout()
            => Service.CheckoutAsync(1, Guid.NewGuid().ToString(), default);
        /// <summary>Test bazasının resurslarını azad edir.</summary>
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
