using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.PaymentInstructionDetails.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationPhysicalArrivalServiceTests
{
    [Theory]
    [InlineData(ReservationStatus.Confirmed, -5, 10, false, null, true)]
    [InlineData(ReservationStatus.PendingPayment, -5, 10, false, null, false)]
    [InlineData(ReservationStatus.Confirmed, 5, 10, false, null, true)]
    [InlineData(ReservationStatus.Confirmed, -5, -1, false, null, false)]
    [InlineData(ReservationStatus.Confirmed, -5, 10, true, null, false)]
    [InlineData(ReservationStatus.Confirmed, -5, 10, false, -1, false)]
    public void PhysicalArrivalPolicyPreservesEligibility(
        ReservationStatus status, int startOffsetMinutes, int deadlineOffsetMinutes,
        bool arrived, int? vacateOffsetMinutes, bool expected)
    {
        var nowUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var reservation = CreateReservationForPolicy(
            nowUtc, status, startOffsetMinutes, deadlineOffsetMinutes, arrived, vacateOffsetMinutes);

        Assert.Equal(expected, ReservationArrivalPolicy.CanRecordPhysicalArrival(reservation, nowUtc));
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment, -5, 10, false, null, ErrorCode.ReservationSeatingRequiresConfirmation)]
    [InlineData(ReservationStatus.Confirmed, 5, 10, false, null, null)]
    [InlineData(ReservationStatus.Confirmed, -5, -1, false, null, ErrorCode.ReservationSeatingWindowExpired)]
    [InlineData(ReservationStatus.Confirmed, -5, -1, true, null, null)]
    [InlineData(ReservationStatus.Confirmed, -5, 10, true, -1, ErrorCode.ReservationVacateTimeExpired)]
    public void SeatingPolicyPreservesConflictReason(
        ReservationStatus status, int startOffsetMinutes, int deadlineOffsetMinutes,
        bool arrived, int? vacateOffsetMinutes, ErrorCode? expected)
    {
        var nowUtc = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var reservation = CreateReservationForPolicy(
            nowUtc, status, startOffsetMinutes, deadlineOffsetMinutes, arrived, vacateOffsetMinutes);

        Assert.Equal(expected, ReservationArrivalPolicy.GetSeatingConflict(reservation, nowUtc));
    }

    /// <summary>Vaxt sərhədlərini sabit UTC anına görə təyin edən test rezervasiyası yaradır.</summary>
    private static Reservation CreateReservationForPolicy(
        DateTime nowUtc, ReservationStatus status, int startOffsetMinutes,
        int deadlineOffsetMinutes, bool arrived, int? vacateOffsetMinutes)
        => new()
        {
            StatusId = StatusIds.Reservation(status),
            ReservedAt = nowUtc.AddMinutes(startOffsetMinutes),
            NoShowDeadlineAt = nowUtc.AddMinutes(deadlineOffsetMinutes),
            ArrivedAt = arrived ? nowUtc.AddMinutes(-1) : null,
            MustVacateAt = vacateOffsetMinutes.HasValue
                ? nowUtc.AddMinutes(vacateOffsetMinutes.Value)
                : null
        };

    [Fact]
    public async Task MarkArrivalRecordsPresenceWithoutOpeningTable()
    {
        var fixture = new Fixture(RoleCode.Manager);

        var response = await fixture.Service.MarkArrivalAsync(2, 1);

        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), response.StatusId);
        Assert.NotNull(fixture.Reservation.ArrivedAt);
        Assert.Equal(5, fixture.Reservation.ArrivedByUserId);
        Assert.Null(fixture.Reservation.SeatedAt);
        Assert.Empty(fixture.Reservation.StatusHistory);
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
        fixture.Notifications.Verify(service => service.CreateAsync(
            It.Is<CreateNotificationRequest>(request =>
                request.TypeId == (int)NotificationType.ReservationArrived &&
                request.UserId == 4)), Times.Once);
    }

    [Fact]
    public async Task RepeatedArrivalDoesNotSendAnotherNotification()
    {
        var fixture = new Fixture(RoleCode.Waiter);

        await fixture.Service.MarkArrivalAsync(2, 1);
        var firstArrivalAt = fixture.Reservation.ArrivedAt;
        await fixture.Service.MarkArrivalAsync(2, 1);

        Assert.Equal(firstArrivalAt, fixture.Reservation.ArrivedAt);
        fixture.Notifications.Verify(service => service.CreateAsync(
            It.IsAny<CreateNotificationRequest>()), Times.Once);
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
    }

    [Fact]
    public async Task WaiterSeatsPreviouslyArrivedGuestAfterOriginalNoShowDeadline()
    {
        var fixture = new Fixture(RoleCode.Waiter);
        fixture.Reservation.NoShowDeadlineAt = DateTime.UtcNow.AddMinutes(-1);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-5);
        fixture.Reservation.ArrivedByUserId = 6;

        var response = await fixture.Service.CheckInReservationAsync(2, 1);

        Assert.Equal(StatusIds.Reservation(ReservationStatus.Seated), response.StatusId);
        Assert.Equal(5, fixture.Reservation.WaiterUserId);
        Assert.Equal(6, fixture.Reservation.ArrivedByUserId);
        Assert.NotNull(fixture.Reservation.SeatedAt);
        fixture.Sessions.Verify(repository => repository.Add(It.Is<TableSession>(session =>
            session.ReservationId == 1 && session.WaiterUserId == 5)), Times.Once);
    }

    [Fact]
    public async Task DirectSeatingAlsoRecordsArrival()
    {
        var fixture = new Fixture(RoleCode.Waiter);

        await fixture.Service.CheckInReservationAsync(2, 1);

        Assert.NotNull(fixture.Reservation.ArrivedAt);
        Assert.Equal(5, fixture.Reservation.ArrivedByUserId);
        Assert.NotNull(fixture.Reservation.SeatedAt);
    }

    [Fact]
    public async Task ManagerCannotSeatGuest()
    {
        var fixture = new Fixture(RoleCode.Manager);

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.CheckInReservationAsync(2, 1));

        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
    }

    [Fact]
    public async Task ArrivalCannotBeMarkedAfterRestaurantCloses()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservations.Setup(repository => repository.IsRestaurantOpenAsync(
            2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.MarkArrivalAsync(2, 1));

        Assert.Null(fixture.Reservation.ArrivedAt);
    }

    [Fact]
    public async Task EarlyArrivalNotifiesOnlyAssignedRestaurantWaiters()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.ReservedAt = DateTime.UtcNow.AddMinutes(10);
        fixture.Reservation.NoShowDeadlineAt = DateTime.UtcNow.AddMinutes(25);
        fixture.Staff.Setup(repository => repository.GetActiveByRestaurantAndRolesAsync(2,
                It.Is<IReadOnlyCollection<int>>(roles => roles.Contains((int)RoleCode.Waiter))))
            .ReturnsAsync([new UserRestaurant { UserId = 6, RestaurantId = 2 }, new UserRestaurant { UserId = 6, RestaurantId = 2 }]);
        await fixture.Service.MarkArrivalAsync(2, 1);
        Assert.NotNull(fixture.Reservation.ArrivedAt);
        fixture.Notifications.Verify(service => service.CreateAsync(It.Is<CreateNotificationRequest>(request =>
            request.UserId == 6 && request.RestaurantId == 2 && request.TypeId == (int)NotificationType.ReservationArrived &&
            !request.PayloadJson!.Contains("deposit", StringComparison.OrdinalIgnoreCase))), Times.Once);
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
    }

    [Fact]
    public async Task EarlyArrivalOutsideBookingServiceIntervalIsRejected()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservations.Setup(repository => repository.IsWithinReservationServiceIntervalAsync(
            2, 1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.MarkArrivalAsync(2, 1));
        Assert.Null(fixture.Reservation.ArrivedAt);
    }

    [Fact]
    public async Task EarlySeatingAndRetryCreateOnlyOneSession()
    {
        var fixture = new Fixture(RoleCode.Waiter);
        fixture.Reservation.ReservedAt = DateTime.UtcNow.AddMinutes(10);
        fixture.Reservation.NoShowDeadlineAt = DateTime.UtcNow.AddMinutes(25);
        await fixture.Service.CheckInReservationAsync(2, 1);
        await fixture.Service.CheckInReservationAsync(2, 1);
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Once);
        Assert.NotNull(fixture.Reservation.ArrivedAt);
        Assert.Single(fixture.Reservation.StatusHistory);
    }

    [Fact]
    public async Task EarlySeatingCannotDisplaceAnInterveningReservation()
    {
        var fixture = new Fixture(RoleCode.Waiter);
        fixture.Reservation.ReservedAt = DateTime.UtcNow.AddMinutes(30);
        fixture.Reservation.NoShowDeadlineAt = DateTime.UtcNow.AddMinutes(45);
        fixture.Reservation.MustVacateAt = DateTime.UtcNow.AddHours(2);
        fixture.Tables.Setup(repository => repository.GetReservationTableAvailabilityAsync(
            2, 3, It.IsAny<DateTimeOffset>(), 1)).ReturnsAsync(new ReservationTableAvailability(
                new Table { Id = 3, IsActive = true, Capacity = 2 }, DateTime.UtcNow.AddMinutes(15)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.CheckInReservationAsync(2, 1));
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
    }

    [Theory]
    [InlineData(ReservationDepartureReason.RestaurantCannotSeat, true)]
    [InlineData(ReservationDepartureReason.GuestLeft, false)]
    public async Task DepartureReasonDeterminesRefundWithoutBecomingNoShow(ReservationDepartureReason reason, bool refund)
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-5);
        fixture.Reservation.CancellationDeadline = DateTime.UtcNow.AddHours(-1);
        fixture.Reservation.DepositAmount = 100;
        fixture.Reservation.PaymentProofs.Add(new ReservationPaymentProof
        {
            Id = 1, Amount = 100, StatusId = StatusIds.Reservation(ReservationStatus.Confirmed)
        });
        await fixture.Service.RecordWaitingDepartureAsync(2, 1,
            new ReservationDepartureRequest { ReasonCode = reason, Reason = "Test reason" });
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Cancelled), fixture.Reservation.StatusId);
        Assert.Equal(reason, fixture.Reservation.DepartureReason);
        Assert.Equal(refund, fixture.Reservation.RefundEligible);
        Assert.Equal(5, fixture.Reservation.CancelledByUserId);
        Assert.Null(fixture.Reservation.NoShowAt);
    }

    [Fact]
    public async Task DepartureRequiresWaitingStateAndReason()
    {
        var fixture = new Fixture(RoleCode.Manager);
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.RecordWaitingDepartureAsync(2, 1,
            new ReservationDepartureRequest { ReasonCode = (ReservationDepartureReason)0, Reason = "" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.RecordWaitingDepartureAsync(2, 1,
            new ReservationDepartureRequest { ReasonCode = ReservationDepartureReason.GuestLeft, Reason = "Test" }));
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), fixture.Reservation.StatusId);
    }

    [Fact]
    public async Task WaiterCannotResolveDeparture()
    {
        var fixture = new Fixture(RoleCode.Waiter);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.RecordWaitingDepartureAsync(2, 1,
            new ReservationDepartureRequest { ReasonCode = ReservationDepartureReason.GuestLeft, Reason = "Test" }));
    }

    [Fact]
    public async Task TableChangePreservesTermsAndDoesNotSeatGuest()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-1);
        fixture.Reservation.DepositAmount = 100;
        var reservedAt = fixture.Reservation.ReservedAt;
        var deadline = fixture.Reservation.NoShowDeadlineAt;
        fixture.Tables.Setup(repository => repository.GetReservationTableAvailabilityAsync(
            2, 4, It.IsAny<DateTimeOffset>(), 1)).ReturnsAsync(new ReservationTableAvailability(
                new Table { Id = 4, RestaurantId = 2, IsActive = true, Capacity = 2, Name = "Masa 4" }, null));
        await fixture.Service.ChangeWaitingTableAsync(2, 1,
            new ReservationTableChangeRequest { TableId = 4, CustomerConsent = true });
        Assert.Equal(4, fixture.Reservation.TableId);
        Assert.Equal(reservedAt, fixture.Reservation.ReservedAt);
        Assert.Equal(deadline, fixture.Reservation.NoShowDeadlineAt);
        Assert.Equal(100, fixture.Reservation.DepositAmount);
        Assert.Null(fixture.Reservation.SeatedAt);
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
        fixture.Tables.Verify(repository => repository.AcquireReservationLockAsync(2, 3, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Tables.Verify(repository => repository.AcquireReservationLockAsync(2, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TableChangeRequiresConsentAndCannotShortenExitTime()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-1);
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ChangeWaitingTableAsync(2, 1,
            new ReservationTableChangeRequest { TableId = 4, CustomerConsent = false }));
        fixture.Tables.Setup(repository => repository.GetReservationTableAvailabilityAsync(
            2, 4, It.IsAny<DateTimeOffset>(), 1)).ReturnsAsync(new ReservationTableAvailability(
                new Table { Id = 4, IsActive = true, Capacity = 2 }, DateTime.UtcNow.AddHours(1)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.ChangeWaitingTableAsync(2, 1,
            new ReservationTableChangeRequest { TableId = 4, CustomerConsent = true }));
        Assert.Equal(3, fixture.Reservation.TableId);
    }

    [Fact]
    public async Task WaitingEscalationIsOneTimeAndDoesNotCancelReservation()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.ReservedAt = DateTime.UtcNow.AddMinutes(-20);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-25);
        fixture.Reservations.Setup(repository => repository.GetWaitingEscalationCandidatesAsync(
            It.IsAny<DateTime>(), 15, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReservationWaitingCandidate(1, 2)]);
        fixture.Staff.Setup(repository => repository.GetActiveByRestaurantAndRolesAsync(2,
            It.Is<IReadOnlyCollection<int>>(roles => roles.Contains((int)RoleCode.Manager) && roles.Contains((int)RoleCode.Owner))))
            .ReturnsAsync([new UserRestaurant { UserId = 6, RestaurantId = 2 }]);
        Assert.Equal(1, await fixture.Service.EscalateWaitingReservationsAsync(100));
        Assert.Equal(0, await fixture.Service.EscalateWaitingReservationsAsync(100));
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), fixture.Reservation.StatusId);
        Assert.NotNull(fixture.Reservation.WaitingEscalatedAt);
        fixture.Notifications.Verify(service => service.CreateAsync(It.Is<CreateNotificationRequest>(request =>
            request.UserId == 6 && request.TypeId == (int)NotificationType.ReservationWaitingEscalated)), Times.Once);
    }

    [Fact]
    public async Task SeatRequestCannotUseOldTableLockAfterParallelTableChange()
    {
        var fixture = new Fixture(RoleCode.Waiter);
        fixture.Reservations.Setup(repository => repository.GetByIdForRestaurantSnapshotAsync(
            1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(new Reservation { Id = 1, RestaurantId = 2, TableId = 3 });
        fixture.Reservation.TableId = 4;
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.CheckInReservationAsync(2, 1));
        fixture.Sessions.Verify(repository => repository.Add(It.IsAny<TableSession>()), Times.Never);
    }

    [Fact]
    public async Task WaitingWorkerRechecksSeatingAfterAcquiringLock()
    {
        var fixture = new Fixture(RoleCode.Manager);
        fixture.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.Seated);
        fixture.Reservation.ArrivedAt = DateTime.UtcNow.AddMinutes(-20);
        fixture.Reservation.SeatedAt = DateTime.UtcNow;
        fixture.Reservations.Setup(repository => repository.GetWaitingEscalationCandidatesAsync(
            It.IsAny<DateTime>(), 15, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReservationWaitingCandidate(1, 2)]);
        Assert.Equal(0, await fixture.Service.EscalateWaitingReservationsAsync(100));
        fixture.Notifications.Verify(service => service.CreateAsync(It.IsAny<CreateNotificationRequest>()), Times.Never);
        Assert.Null(fixture.Reservation.WaitingEscalatedAt);
    }

    private sealed class Fixture
    {
        public Reservation Reservation { get; } = new()
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            PeopleCount = 2, ReservedAt = DateTime.UtcNow.AddMinutes(-5),
            NoShowDeadlineAt = DateTime.UtcNow.AddMinutes(10)
        };

        public Mock<IReservationRepository> Reservations { get; } = new();
        public Mock<ITableSessionRepository> Sessions { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<ITableRepository> Tables { get; } = new();
        public Mock<IUserRestaurantRepository> Staff { get; } = new();
        public ReservationManager Service { get; }

        public Fixture(RoleCode role)
        {
            Reservations.Setup(repository => repository.GetByIdForRestaurantSnapshotAsync(
                1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            Reservations.Setup(repository => repository.GetByIdForRestaurantAsync(
                1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            Reservations.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
            Reservations.Setup(repository => repository.IsRestaurantOpenAsync(
                2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Reservations.Setup(repository => repository.IsWithinReservationServiceIntervalAsync(
                2, 1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var staff = Staff;
            staff.Setup(repository => repository.UserBelogsToRestaurantAsync(5, 2)).ReturnsAsync(true);
            staff.Setup(repository => repository.GetActiveByRestaurantAndRolesAsync(2, It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync([]);

            var table = Tables;
            table.Setup(repository => repository.AcquireReservationLockAsync(
                2, 3, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            table.Setup(repository => repository.HasOpenTableSessionAsync(2, 3)).ReturnsAsync(false);
            table.Setup(repository => repository.GetReservationTableAvailabilityAsync(
                    2, 3, It.IsAny<DateTimeOffset>(), 1))
                .ReturnsAsync(new ECafe.Application.Repositories.Table.ReservationTableAvailability(
                    new Table { Id = 3, RestaurantId = 2, IsActive = true, Capacity = 2 }, null));
            Sessions.Setup(repository => repository.Add(It.IsAny<TableSession>()))
                .ReturnsAsync((TableSession session) => session);

            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
            transaction.Setup(value => value.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(value => value.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var workflow = new Mock<IWorkflowActionService>();
            workflow.Setup(value => value.EnsureCanExecuteAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>())).Returns(Task.CompletedTask);
            Notifications.Setup(value => value.CreateAsync(It.IsAny<CreateNotificationRequest>()))
                .Returns(Task.CompletedTask);
            var audit = new Mock<IAuditLogService>();
            audit.Setup(value => value.RecordRestaurantActionAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);

            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("userId", "5"),
                    new Claim("restaurantIds", "2"),
                    new Claim(ClaimTypes.Role, ((int)role).ToString())
                ], "test"))
            };

            Service = new ReservationManager(
                new HttpContextAccessor { HttpContext = context }, Mock.Of<IMapper>(),
                Mock.Of<IConfiguration>(), table.Object, Sessions.Object,
                Mock.Of<IRestaurantRepository>(), Mock.Of<IRestaurantDepositService>(),
                Mock.Of<IRestaurantContractRepository>(), Reservations.Object, staff.Object,
                Notifications.Object, audit.Object, transactions.Object,
                Mock.Of<IReservationPaymentInstructionRepository>(),
                Mock.Of<IReservationPaymentProofRepository>(), Mock.Of<IFileRepository>(),
                Mock.Of<IFileAccessUrlService>(), workflow.Object,
                Mock.Of<IPaymentInstructionDetailsProtector>(),
                Mock.Of<ILogger<ReservationManager>>(), Options.Create(new ReservationTimingOptions()));
        }
    }
}
