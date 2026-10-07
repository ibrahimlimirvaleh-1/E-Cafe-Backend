using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Notification;
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
    [InlineData(ReservationStatus.Confirmed, 5, 10, false, null, false)]
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
    [InlineData(ReservationStatus.Confirmed, 5, 10, false, null, ErrorCode.ReservationSeatingBeforeStart)]
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

            var staff = new Mock<IUserRestaurantRepository>();
            staff.Setup(repository => repository.UserBelogsToRestaurantAsync(5, 2)).ReturnsAsync(true);

            var table = new Mock<ITableRepository>();
            table.Setup(repository => repository.AcquireReservationLockAsync(
                2, 3, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            table.Setup(repository => repository.HasOpenTableSessionAsync(2, 3)).ReturnsAsync(false);
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
