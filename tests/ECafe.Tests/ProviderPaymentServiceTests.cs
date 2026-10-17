using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Payments;
using ECafe.Application.Services.Payments.Concrete;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.ReservationRefund;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed class ProviderPaymentServiceTests
{
    [Fact]
    public async Task RestaurantAuthorizationPreservesResponseAndHoldPolicyWithoutCollectingDetails()
    {
        await using var f = await Fixture.CreateAsync();
        f.Login(2, RoleCode.Manager);
        var result = await f.Service.AuthorizeDepositAsync(1, 1, default);
        Assert.False(result.CanAuthorize);
        var reservation = await f.Context.Reservations.SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.PendingPayment), reservation.StatusId);
        Assert.Equal(f.Clock.Now.AddMinutes(15).UtcDateTime, reservation.HoldExpiresAt);
        Assert.Equal(20, reservation.DepositAmount);
        Assert.Empty(f.Context.ReservationPaymentInstructions);
        Assert.Empty(f.Context.ReservationPaymentProofs);
        Assert.Empty(f.Context.Payments);
        f.Clock.Now = f.Clock.Now.AddMinutes(2);
        await f.Service.AuthorizeDepositAsync(1, 1, default);
        Assert.Equal(Fixture.Now.AddMinutes(15).UtcDateTime, reservation.HoldExpiresAt);
        f.Login(1, RoleCode.Customer);
        Assert.True((await f.Service.GetReservationAsync(1, default)).CanPay);
    }

    [Fact]
    public async Task CheckoutRequiresRestaurantApprovalAndPositiveDeposit()
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        await f.Authorize();
        (await f.Context.Reservations.SingleAsync()).DepositAmount = 0;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        Assert.Empty(f.Context.Payments);
    }

    [Fact]
    public async Task SameKeyReplaysOnePaymentAndAnotherPendingKeyIsRejected()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        var key = Guid.NewGuid().ToString();
        var first = await f.Service.CreateCheckoutAsync(1, key, default);
        var replay = await f.Service.CreateCheckoutAsync(1, key, default);
        Assert.Equal(first.Id, replay.Id);
        Assert.True(first.IsTest);
        Assert.Null(first.CheckoutUrl);
        Assert.Equal(20, first.Amount);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        Assert.Single(f.Context.Payments);
        f.Audit.Verify(service => service.RecordRestaurantActionAsync(1, AuditActions.ProviderCheckoutCreated,
            It.IsAny<object>(), AuditEntityTypes.Reservation, 1, null), Times.Once);
    }

    [Fact]
    public async Task SuccessConfirmsOnceWithOneNotificationAndNoReceipt()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        var payment = await f.Checkout();
        var first = await f.Service.SimulateAsync(payment.Id, ProviderOperationStatus.Succeeded, default);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var replay = await f.Service.SimulateAsync(payment.Id, ProviderOperationStatus.Succeeded, default);
        var refreshed = await f.Service.RefreshAsync(payment.Id, default);
        Assert.Equal(first.PaidAt, replay.PaidAt);
        Assert.Equal(first.PaidAt, refreshed.PaidAt);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), (await f.Context.Reservations.SingleAsync()).StatusId);
        Assert.Empty(f.Context.ReservationPaymentProofs);
        Assert.Empty(f.Context.ReservationPaymentInstructions);
        f.Notifications.Verify(service => service.CreateAsync(It.Is<CreateNotificationRequest>(request =>
            request.UserId == 1 && request.TypeId == (int)NotificationType.ReservationConfirmed)), Times.Once);
        f.Audit.Verify(service => service.RecordRestaurantActionAsync(1, AuditActions.ProviderPaymentUpdated,
            It.IsAny<object>(), AuditEntityTypes.Reservation, 1, null), Times.Once);
    }

    [Theory]
    [InlineData(ProviderOperationStatus.Failed)]
    [InlineData(ProviderOperationStatus.Cancelled)]
    public async Task FailedOrCancelledAttemptCanBeRetriedWithNewKeyWithoutLosingOldResult(ProviderOperationStatus outcome)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        var first = await f.Checkout();
        await f.Service.SimulateAsync(first.Id, outcome, default);
        await f.Service.RefreshAsync(first.Id, default);
        var next = await f.Checkout();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(2, await f.Context.Payments.CountAsync());
        Assert.Equal(StatusIds.Reservation(ReservationStatus.PendingPayment), (await f.Context.Reservations.SingleAsync()).StatusId);
    }

    [Fact]
    public async Task LateCapturedPaymentStartsCompensationWithoutResurrectingExpiredReservation()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        var checkout = await f.Checkout();
        var reservation = await f.Context.Reservations.SingleAsync();
        reservation.StatusId = StatusIds.Reservation(ReservationStatus.Expired);
        await f.Context.SaveChangesAsync();
        f.Clock.Now = f.Clock.Now.AddMinutes(20);
        var result = await f.Service.SimulateAsync(checkout.Id, ProviderOperationStatus.Succeeded, default);
        Assert.True(result.RequiresRefund);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Expired), reservation.StatusId);
        var refund = await f.Context.ReservationRefunds.SingleAsync();
        Assert.Equal(checkout.Id, refund.SourcePaymentId);
        Assert.Equal(StatusIds.Refund(RefundStatus.Processing), refund.StatusId);
        Assert.Null(refund.RefundedAt);
    }

    [Fact]
    public async Task RefundReusesOriginalPaymentCompletesOnceAndNeverCollectsPayoutDetails()
    {
        await using var f = await Fixture.CreateAsync();
        var payment = await f.PayAndCancel();
        var first = await f.Service.RequestRefundAsync(1, default);
        var replay = await f.Service.RequestRefundAsync(1, default);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(payment.Id, first.SourcePaymentId);
        Assert.True(first.IsTest);
        Assert.True(first.CanSimulate);
        Assert.False(first.RequiresManualReview);
        Assert.Equal(StatusIds.Refund(RefundStatus.Processing), first.StatusId);
        Assert.Null(first.RefundedAt);
        var done = await f.Service.SimulateRefundAsync(first.Id, ProviderOperationStatus.Succeeded, default);
        f.Clock.Now = f.Clock.Now.AddHours(1);
        var again = await f.Service.SimulateRefundAsync(first.Id, ProviderOperationStatus.Succeeded, default);
        Assert.Equal(done.RefundedAt, again.RefundedAt);
        Assert.Equal(StatusIds.Refund(RefundStatus.Refunded), done.StatusId);
        Assert.Single(f.Context.ReservationRefunds);
        Assert.Empty(f.Context.ReservationRefundPayoutDetails);
        Assert.Empty(f.Context.ReservationRefundTransfers);
        Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Refunded), (await f.Context.Payments.SingleAsync()).PaymentStatusId);
    }

    [Fact]
    public async Task FailedRefundDoesNotMarkMoneyReturned()
    {
        await using var f = await Fixture.CreateAsync();
        await f.PayAndCancel();
        var refund = await f.Service.RequestRefundAsync(1, default);
        var failed = await f.Service.SimulateRefundAsync(refund.Id, ProviderOperationStatus.Failed, default);
        Assert.Equal(StatusIds.Refund(RefundStatus.Failed), failed.StatusId);
        Assert.Null(failed.RefundedAt);
        Assert.Null((await f.Context.Payments.SingleAsync()).RefundedAt);
    }

    [Fact]
    public async Task LegacyDepositEntersManualReviewAndCannotBecomeFakeProviderRefund()
    {
        await using var f = await Fixture.CreateAsync();
        var reservation = await f.Context.Reservations.SingleAsync();
        reservation.StatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
        reservation.RefundEligible = true;
        reservation.PaymentProofs.Add(new ReservationPaymentProof { Amount = 20, StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), SubmittedAt = f.Clock.Now.UtcDateTime });
        await f.Context.SaveChangesAsync();
        var refund = await f.Service.RequestRefundAsync(1, default);
        Assert.True(refund.RequiresManualReview);
        Assert.False(refund.IsTest);
        Assert.False(refund.CanSimulate);
        Assert.Null(refund.SourcePaymentId);
        Assert.Equal(StatusIds.Refund(RefundStatus.Requested), refund.StatusId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateRefundAsync(refund.Id, ProviderOperationStatus.Succeeded, default));
    }

    [Theory]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task NonManagersCannotAuthorize(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        f.Login(2, role);
        (await f.Context.UserRestaurants.SingleAsync()).RoleId = (int)role;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.AuthorizeDepositAsync(1, 1, default));
    }

    [Fact]
    public async Task AnotherCustomerCannotReadPayOrCompleteSomeoneElsesPayment()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        var payment = await f.Checkout();
        f.Login(3, RoleCode.Customer);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.GetReservationAsync(1, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Checkout());
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SimulateAsync(payment.Id, ProviderOperationStatus.Succeeded, default));
    }

    [Fact]
    public async Task RevokedManagerAssignmentIsRecheckedAfterTableLock()
    {
        await using var f = await Fixture.CreateAsync();
        f.Login(2, RoleCode.Manager);
        f.BeforeLock = async () => { (await f.Context.UserRestaurants.SingleAsync()).IsActive = false; await f.Context.SaveChangesAsync(); };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.AuthorizeDepositAsync(1, 1, default));
        Assert.Empty(f.Context.Payments);
    }

    [Fact]
    public async Task UnknownEnvironmentBlocksPaymentAndRefundSimulationEvenWhenConfiguredTrue()
    {
        await using var f = await Fixture.CreateAsync("Unknown");
        Assert.False(f.Service.GetCapabilities().Enabled);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateAsync(1, ProviderOperationStatus.Succeeded, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateRefundAsync(1, ProviderOperationStatus.Succeeded, default));
        Assert.Empty(f.Context.Payments);
    }

    [Fact]
    public async Task ProductionHostedTestServerCanSimulatePaymentAndRefund()
    {
        await using var f = await Fixture.CreateAsync("Production");
        Assert.True(f.Service.GetCapabilities().Enabled);
        Assert.True(f.Service.GetCapabilities().IsTest);
        var payment = await f.PayAndCancel();
        Assert.True(payment.IsTest);
        var refund = await f.Service.RequestRefundAsync(1, default);
        Assert.True(refund.IsTest);
        var result = await f.Service.SimulateRefundAsync(refund.Id, ProviderOperationStatus.Succeeded, default);
        Assert.Equal(StatusIds.Refund(RefundStatus.Refunded), result.StatusId);
        Assert.Single(f.Context.Payments);
        Assert.Single(f.Context.ReservationRefunds);
    }

    [Fact]
    public async Task OwnershipIsRecheckedAfterLockBeforeCheckoutCreation()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        f.BeforeLock = async () => { (await f.Context.Reservations.SingleAsync()).CustomerUserId = 3; await f.Context.SaveChangesAsync(); };
        await Assert.ThrowsAsync<NotFoundException>(() => f.Checkout());
        Assert.Empty(f.Context.Payments);
    }

    [Fact]
    public async Task ExpiredHoldAndInvalidKeyCannotCreateNewAttempt()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Authorize();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.CreateCheckoutAsync(1, "invalid", default));
        f.Clock.Now = f.Clock.Now.AddMinutes(15);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Checkout());
        Assert.Empty(f.Context.Payments);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task TestEndpointCannotApplyToNonTestSource(string environment)
    {
        await using var f = await Fixture.CreateAsync(environment);
        await f.Authorize();
        var checkout = await f.Checkout();
        (await f.Context.Payments.SingleAsync()).IsTest = false;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateAsync(checkout.Id, ProviderOperationStatus.Succeeded, default));
        Assert.Null((await f.Context.Payments.SingleAsync()).PaidAt);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task TestRefundEndpointCannotApplyToNonTestSource(string environment)
    {
        await using var f = await Fixture.CreateAsync(environment);
        await f.PayAndCancel();
        var refund = await f.Service.RequestRefundAsync(1, default);
        (await f.Context.Payments.SingleAsync()).IsTest = false;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SimulateRefundAsync(refund.Id, ProviderOperationStatus.Succeeded, default));
        Assert.Null((await f.Context.Payments.SingleAsync()).RefundedAt);
        Assert.Null((await f.Context.ReservationRefunds.SingleAsync()).RefundedAt);
    }

    [Fact]
    public async Task RefundRefreshDoesNotPretendPendingMoneyWasReturned()
    {
        await using var f = await Fixture.CreateAsync();
        await f.PayAndCancel();
        var refund = await f.Service.RequestRefundAsync(1, default);
        var refreshed = await f.Service.RefreshRefundAsync(refund.Id, default);
        Assert.Equal(StatusIds.Refund(RefundStatus.Processing), refreshed.StatusId);
        Assert.Null(refreshed.RefundedAt);
        Assert.Null((await f.Context.Payments.SingleAsync()).RefundedAt);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);
        public ECafeDbContext Context { get; }
        public ProviderPaymentManager Service { get; }
        public DefaultHttpContext Http { get; } = new();
        public MutableClock Clock { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Func<Task>? BeforeLock { get; set; }

        /// <summary>Həqiqi repository və test adapterini birləşdirir; yalnız SQL kilid/transaction sərhədlərini yaddaş testində əvəz edir.</summary>
        private Fixture(ECafeDbContext context, string environment)
        {
            Context = context;
            Login(1, RoleCode.Customer);
            var reservationRepo = new Mock<IReservationRepository>();
            reservationRepo.Setup(repo => repo.GetForProviderPaymentAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns((int id, bool tracked, CancellationToken token) => (tracked ? context.Reservations.AsQueryable() : context.Reservations.AsNoTracking())
                    .Include(item => item.Restaurant).Include(item => item.Payments).Include(item => item.PaymentProofs)
                    .Include(item => item.StatusHistory).Include(item => item.Refunds).SingleOrDefaultAsync(item => item.Id == id, token));
            reservationRepo.Setup(repo => repo.SaveChangesAsync()).Returns(() => context.SaveChangesAsync());
            var tableRepo = new Mock<ITableRepository>();
            tableRepo.Setup(repo => repo.AcquireReservationLockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { if (BeforeLock != null) await BeforeLock(); });
            tableRepo.Setup(repo => repo.GetReservationTableAvailabilityAsync(1, 1, It.IsAny<DateTimeOffset>(), 1))
                .ReturnsAsync(new ReservationTableAvailability(new Table { Id = 1, RestaurantId = 1 }, null));
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(factory => factory.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<IApplicationDbTransaction>());
            Service = new ProviderPaymentManager(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == Http), Mock.Of<IMapper>(),
                new ConfigurationBuilder().Build(), reservationRepo.Object, new BaseRepository<Payment>(context), new ReservationRefundRepository(context),
                tableRepo.Object, new UserRepository(context), new UserRestaurantRepository(context), transactions.Object, Mock.Of<IWorkflowActionService>(),
                new SimulationPaymentGateway(), new PaymentRuntime(Options.Create(new PaymentOptions { Mode = "Simulation", EnableSimulation = true }), new(environment)),
                Options.Create(new ReservationTimingOptions()), Clock, Notifications.Object, Mock.Of<IReservationRefundNotifier>(), Audit.Object, Mock.Of<IFileAccessUrlService>());
        }

        /// <summary>Bir restoran, aktiv menecer və iki müştəri ilə ayrı yaddaş bazası yaradır.</summary>
        public static async Task<Fixture> CreateAsync(string environment = "Testing")
        {
            var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            await context.Database.EnsureCreatedAsync();
            context.Restaurants.Add(new Restaurant { Id = 1, Name = "Test", Location = "Baku", Phone = "1", TimeZone = "Asia/Baku", IsActive = true, PaymentHoldMinutes = 15 });
            context.Tables.Add(new Table { Id = 1, RestaurantId = 1, TableNo = 1, Capacity = 4, IsActive = true });
            for (var id = 1; id <= 3; id++) context.Users.Add(new User { Id = id, Name = "Test", Surname = id.ToString(), Email = $"{id}@example.test", Phone = id.ToString(), Password = "test", IsActive = true });
            context.UserRestaurants.Add(new UserRestaurant { Id = 1, RestaurantId = 1, UserId = 2, RoleId = (int)RoleCode.Manager, IsActive = true });
            context.Reservations.Add(new Reservation { Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1, PeopleCount = 2,
                StatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction), DepositAmount = 20,
                ReservedAt = Now.AddHours(3).UtcDateTime, RestaurantResponseExpiresAt = Now.AddMinutes(15).UtcDateTime,
                CancellationDeadline = Now.AddHours(1).UtcDateTime });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(context, environment);
        }

        /// <summary>Sorğunun istifadəçi və seçilmiş restoran kontekstini dəyişir; cari rol ayrıca bazada yoxlanılır.</summary>
        public void Login(int id, RoleCode role)
            => Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", id.ToString()),
                new Claim(ClaimTypes.Role, ((int)role).ToString()), new Claim("restaurantId", "1")], "test"));

        /// <summary>Restoranın icazəsindən sonra müştəri kontekstinə qayıdır.</summary>
        public async Task Authorize()
        {
            Login(2, RoleCode.Manager);
            await Service.AuthorizeDepositAsync(1, 1, default);
            Login(1, RoleCode.Customer);
        }

        /// <summary>Yeni müstəqil cəhd üçün yeni UUID ilə checkout başladır.</summary>
        public Task<ECafe.Application.DTOs.Payment.CheckoutPaymentResponse> Checkout()
            => Service.CreateCheckoutAsync(1, Guid.NewGuid().ToString(), default);

        /// <summary>Təsdiqlənmiş test depoziti və mövcud biznes qaydasının saxlanmış refund hüququnu hazırlayır.</summary>
        public async Task<ECafe.Application.DTOs.Payment.CheckoutPaymentResponse> PayAndCancel()
        {
            await Authorize();
            var payment = await Checkout();
            await Service.SimulateAsync(payment.Id, ProviderOperationStatus.Succeeded, default);
            var reservation = await Context.Reservations.SingleAsync();
            reservation.StatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
            reservation.RefundEligible = true;
            await Context.SaveChangesAsync();
            return payment;
        }

        /// <summary>Test bazasının context resurslarını azad edir.</summary>
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Fixture.Now;
        /// <summary>Müddət sərhədlərini gözləmədən yoxlamaq üçün sabitləşdirilmiş UTC vaxtını qaytarır.</summary>
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
