using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationArrivalServiceTests
{
    private sealed class FixedClock : TimeProvider
    {
        public DateTime Now { get; set; } = ReservationTimingPolicyTests.Start;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Fixture
    {
        public Reservation Reservation { get; } = ReservationTimingPolicyTests.CreateReservation();
        public Mock<IReservationRepository> Reservations { get; } = new();
        public Mock<ITableRepository> Tables { get; } = new();
        public Mock<IRestaurantRepository> Restaurants { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public FixedClock Clock { get; } = new();
        public ReservationArrivalManager Service { get; }

        public Fixture()
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "4")], "test"))
            };
            Reservations.Setup(r => r.GetForArrivalAdjustmentAsync(1, 4, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Reservation);
            Restaurants.Setup(r => r.HasRestaurantActiveContractAsync(2)).ReturnsAsync(true);
            var assignments = new Mock<IUserRestaurantRepository>();
            assignments.Setup(r => r.GetActiveByRestaurantAndRolesAsync(2, It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync([new UserRestaurant { UserId = 5 }, new UserRestaurant { UserId = 6 }]);
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            Service = new ReservationArrivalManager(new HttpContextAccessor { HttpContext = context },
                Mock.Of<IMapper>(), Mock.Of<IConfiguration>(), Reservations.Object, Tables.Object,
                Restaurants.Object, assignments.Object, transactions.Object, Notifications.Object,
                Mock.Of<IAuditLogService>(), Options.Create(new ReservationTimingOptions()), Clock);
        }
    }

    [Fact]
    public async Task OfferAndAccept_ChangesOnlyArrivalDeadline_AndNotifiesOnce()
    {
        var f = new Fixture();
        var originalStart = f.Reservation.ReservedAt;
        var offer = await f.Service.OfferAsync(1, new DateTimeOffset(originalStart.AddMinutes(30)), default);
        Assert.False(offer.Accepted);
        Assert.Equal(originalStart.AddMinutes(15), f.Reservation.NoShowDeadlineAt);
        var accepted = await f.Service.AcceptAsync(1, offer.ConsentToken, default);
        Assert.True(accepted.Accepted);
        Assert.Equal(originalStart, f.Reservation.ReservedAt);
        Assert.Equal(originalStart.AddMinutes(45), f.Reservation.NoShowDeadlineAt);
        Assert.Null(f.Reservation.MustVacateAt);
        Assert.Single(f.Reservation.StatusHistory);
        await f.Service.AcceptAsync(1, offer.ConsentToken, default);
        f.Notifications.Verify(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>()), Times.Exactly(3));
        Assert.Single(f.Reservation.StatusHistory);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.OfferAsync(1, offer.ArrivalAt, default));
        Assert.Equal(ErrorCode.ReservationArrivalAlreadyAdjusted, exception.Code);
    }

    [Fact]
    public async Task FreshStatusIsReadAfterWaitingForTableLock()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tables.Setup(t => t.AcquireReservationLockAsync(2, 3, It.IsAny<CancellationToken>())).Returns(gate.Task);
        var pending = f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default);
        f.Reservations.Verify(r => r.GetForArrivalAdjustmentAsync(1, 4, true, It.IsAny<CancellationToken>()), Times.Never);
        f.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.NoShow);
        f.Clock.Now = f.Reservation.NoShowDeadlineAt.AddMinutes(1);
        gate.SetResult();
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => pending);
        Assert.Equal(ErrorCode.ReservationArrivalNotAvailable, exception.Code);
        Assert.Null(f.Reservation.ArrivalAdjustment);
    }

    [Fact]
    public async Task OldConsentToken_CannotAcceptReplacedOffer()
    {
        var f = new Fixture();
        var first = await f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(25)), default);
        await f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, first.ConsentToken, default));
        Assert.Equal(ErrorCode.ReservationArrivalTermsChanged, exception.Code);
        Assert.Null(f.Reservation.ArrivalAdjustment!.AcceptedAt);
    }

    [Fact]
    public async Task NextReservationChanged_RequiresNewConsent()
    {
        var f = new Fixture();
        var offer = await f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default);
        f.Tables.Setup(t => t.GetNextReservationAtAsync(2, 3, 1, f.Reservation.ReservedAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(f.Reservation.ReservedAt.AddMinutes(55));
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, offer.ConsentToken, default));
        Assert.Equal(ErrorCode.ReservationArrivalTermsChanged, exception.Code);
        Assert.Null(f.Reservation.ArrivalAdjustment!.AcceptedAt);
    }

    [Fact]
    public async Task ExpiredConsent_CannotChangeReservation()
    {
        var f = new Fixture();
        var offer = await f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default);
        f.Clock.Now = offer.DecisionExpiresAt.UtcDateTime;
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, offer.ConsentToken, default));
        Assert.Equal(ErrorCode.ReservationArrivalOfferExpired, exception.Code);
        Assert.Equal(f.Reservation.ReservedAt.AddMinutes(15), f.Reservation.NoShowDeadlineAt);
    }

    [Fact]
    public async Task OpenPhysicalSession_PreventsOffer()
    {
        var f = new Fixture();
        f.Tables.Setup(t => t.HasOpenTableSessionAsync(2, 3)).ReturnsAsync(true);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.OfferAsync(1,
            new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default));
        Assert.Equal(ErrorCode.ReservationArrivalNotAvailable, exception.Code);
    }

    [Fact]
    public async Task WrongCustomer_CannotAccessReservation()
    {
        var f = new Fixture();
        f.Reservations.Setup(r => r.GetForArrivalAdjustmentAsync(1, 4, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.GetOptionsAsync(1, default));
    }

    [Fact]
    public async Task PendingScheduleChangePreventsNewDelayOffer()
    {
        var f = new Fixture();
        f.Restaurants.Setup(r => r.HasPendingScheduleChangeAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Assert.False((await f.Service.GetOptionsAsync(1, default)).CanRequest);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.OfferAsync(1,
            new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default));
        Assert.Equal(ErrorCode.ReservationArrivalNotAvailable, error.Code);
        Assert.Null(f.Reservation.ArrivalAdjustment);
    }

    [Fact]
    public async Task ScheduleProposalAfterDelayOfferPreventsAcceptanceOfStaleTerms()
    {
        var f = new Fixture();
        var offer = await f.Service.OfferAsync(1, new DateTimeOffset(f.Reservation.ReservedAt.AddMinutes(30)), default);
        f.Restaurants.Setup(r => r.HasPendingScheduleChangeAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcceptAsync(1, offer.ConsentToken, default));
        Assert.Equal(ErrorCode.ReservationArrivalNotAvailable, error.Code);
        Assert.Null(f.Reservation.ArrivalAdjustment!.AcceptedAt);
    }
}
