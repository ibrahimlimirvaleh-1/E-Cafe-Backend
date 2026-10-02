using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.Table;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantScheduleTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Arrival = new(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc); // 19:00 in Baku.
    private static readonly DateTime NewClose = Arrival.AddHours(2);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Restaurant Restaurant { get; } = new()
        {
            Id = 2, Name = "Test restaurant", Location = "Baku", Phone = "0514300858", IsActive = true,
            WorkingHours = Enum.GetValues<DayOfWeek>().Select(day => new RestaurantWorkingHour
            { DayOfWeek = day, OpensAt = new(9, 0), ClosesAt = new(0, 0), CloseDayOffset = 1 }).ToList()
        };
        public Reservation Reservation { get; }
        public DefaultHttpContext Http { get; } = new();
        public RestaurantScheduleManager Service { get; }
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IUserRestaurantRepository> Assignments { get; } = new();
        public Fixture()
        {
            var user = new User { Id = 4, Name = "Customer", Surname = "Test", Email = "test@example.com", Phone = "0514300858", Password = "test" };
            var table = new Table { Id = 3, Restaurant = Restaurant, TableNo = 1, Name = "Masa-1", Capacity = 2, IsActive = true };
            Reservation = new()
            {
                Id = 1, Restaurant = Restaurant, Table = table, CustomerUser = user, PeopleCount = 2,
                ReservedAt = Arrival, NoShowDeadlineAt = Arrival.AddMinutes(15),
                StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
                DepositAmount = 20, CancellationDeadline = Arrival.AddHours(-1)
            };
            Db.Reservations.Add(Reservation);
            Db.SaveChanges();
            AsManager();
            Assignments.Setup(a => a.GetActiveRoleIdAsync(7, 2)).ReturnsAsync((int)RoleCode.Manager);
            Assignments.Setup(a => a.GetActiveByRestaurantAndRolesAsync(2, It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync([new UserRestaurant { UserId = 7 }]);
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            Service = new(new HttpContextAccessor { HttpContext = Http }, Mock.Of<IMapper>(), Mock.Of<IConfiguration>(),
                new RestaurantScheduleRepository(Db), transactions.Object, Assignments.Object,
                Notifications.Object, Mock.Of<IAuditLogService>(), new Clock());
        }
        public void AsManager(int restaurantId = 2) => SetUser(7, (int)RoleCode.Manager, restaurantId);
        public void AsCustomer(int userId = 4) => SetUser(userId, (int)RoleCode.Customer, null);
        private void SetUser(int userId, int roleId, int? restaurantId) => Http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("userId", userId.ToString()), new Claim("roleId", roleId.ToString()), new Claim(ClaimTypes.Role, roleId.ToString()),
                new Claim("restaurantId", restaurantId?.ToString() ?? "") }, "test"));
        public ProposeScheduleRequest Request(int closesAt = 21) => new(Enum.GetValues<DayOfWeek>().Select(day =>
            new RestaurantWorkingHourDto { DayOfWeek = day, OpensAt = new(9, 0), ClosesAt = new(closesAt, 0), CloseDayOffset = 0 }).ToList(),
            "Restoran həmin günlərdə tez bağlanacaq.");
        public Task<ScheduleChangeResponse> Propose() => Service.ProposeAsync(2, Request(), default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task EarlierClosingAffects19ArrivalEvenThoughArrivalStillFits()
    {
        await using var f = new Fixture();
        var result = await f.Propose();
        var participant = Assert.Single(result.Participants);
        Assert.Equal(NewClose, participant.ProposedVacateAt!.Value.UtcDateTime);
        Assert.True(participant.CanAccept);
        Assert.False(result.CanApply);
        Assert.Equal(TimeOnly.MinValue, f.Restaurant.WorkingHours.First().ClosesAt);
        Assert.Null(f.Reservation.MustVacateAt);
    }

    [Fact]
    public async Task CustomerAcceptanceDoesNotChangeTermsUntilManagerApplies()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        var cancellationDeadline = f.Reservation.CancellationDeadline;
        f.AsCustomer();
        var response = await f.Service.RespondAsync(1, new(proposal.Token, true, null), default);
        Assert.Equal("Accepted", response.State);
        Assert.Null(f.Reservation.MustVacateAt);
        f.AsManager();
        var applied = await f.Service.ApplyAsync(2, new(proposal.Token), default);
        Assert.Equal("Applied", applied.State);
        Assert.Equal(NewClose, f.Reservation.MustVacateAt);
        Assert.Equal(cancellationDeadline, f.Reservation.CancellationDeadline);
        Assert.Equal(Arrival, f.Reservation.ReservedAt);
        Assert.Null(f.Reservation.CancelledAt);
        Assert.Single(f.Reservation.StatusHistory);
        Assert.Equal(new TimeOnly(21, 0), f.Restaurant.WorkingHours.First().ClosesAt);
    }

    [Fact]
    public async Task NoResponseIsNotConsentAndDoesNotApply()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        var response = await f.Service.ApplyAsync(2, new(proposal.Token), default);
        Assert.Equal("Pending", response.State);
        Assert.False(response.CanApply);
        Assert.Null(f.Reservation.MustVacateAt);
    }

    [Fact]
    public async Task DeclineDoesNotCancelReservationOrAffectRefund()
    {
        await using var f = new Fixture();
        f.Reservation.RefundEligible = true;
        var proposal = await f.Propose();
        f.AsCustomer();
        var response = await f.Service.RespondAsync(1, new(proposal.Token, false, null), default);
        Assert.Equal("Rejected", response.State);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), f.Reservation.StatusId);
        Assert.Null(f.Reservation.CancelledAt);
        Assert.True(f.Reservation.RefundEligible);
        f.AsManager();
        Assert.Equal("Pending", (await f.Service.ApplyAsync(2, new(proposal.Token), default)).State);
    }

    [Fact]
    public async Task RetrySameDecisionDoesNotSendDuplicateNotifications()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        f.AsCustomer();
        await f.Service.RespondAsync(1, new(proposal.Token, true, null), default);
        var count = f.Notifications.Invocations.Count;
        await f.Service.RespondAsync(1, new(proposal.Token, true, null), default);
        Assert.Equal(count, f.Notifications.Invocations.Count);
    }

    [Fact]
    public async Task WithdrawalPreservesOriginalTermsAndRejectsOldToken()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        await f.Service.WithdrawAsync(2, new(proposal.Token), default);
        f.AsCustomer();
        Assert.Equal("Withdrawn", (await f.Service.GetCustomerOfferAsync(1, default))!.State);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RespondAsync(1, new(proposal.Token, true, null), default));
        Assert.Equal(ErrorCode.ScheduleOfferNotAvailable, error.Code);
        Assert.Null(f.Reservation.MustVacateAt);
        f.AsManager();
        var next = await f.Propose();
        Assert.NotEqual(proposal.Token, next.Token);
    }

    [Fact]
    public async Task OtherCustomerCannotReadOrAnswerOffer()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        f.AsCustomer(9);
        Assert.Null(await f.Service.GetCustomerOfferAsync(1, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.RespondAsync(1, new(proposal.Token, true, null), default));
    }

    [Fact]
    public async Task ManagerCannotAccessOtherRestaurant()
    {
        await using var f = new Fixture();
        f.AsManager(99);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Propose());
    }

    [Fact]
    public async Task WalkInRequiresRecordedConsentAndDoesNotCloseSession()
    {
        await using var f = new Fixture();
        f.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.Completed);
        var session = new TableSession { Restaurant = f.Restaurant, Table = f.Reservation.Table,
            StatusId = StatusIds.TableSession(TableSessionStatus.Open), OpenedAt = Now };
        f.Db.TableSessions.Add(session);
        await f.Db.SaveChangesAsync();
        var proposal = await f.Propose();
        var participant = Assert.Single(proposal.Participants);
        Assert.Null(participant.ReservationId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.AcknowledgeSessionAsync(2, participant.Id, new(proposal.Token, true, null), default));
        await f.Service.AcknowledgeSessionAsync(2, participant.Id, new(proposal.Token, true, "Müştəri ilə danışıldı, razıdır."), default);
        Assert.Equal("Applied", (await f.Service.ApplyAsync(2, new(proposal.Token), default)).State);
        Assert.Null(session.ClosedAt);
        Assert.Equal(StatusIds.TableSession(TableSessionStatus.Open), session.StatusId);
    }

    [Fact]
    public async Task AcceptedDelayAfterNewClosingCannotBeAccepted()
    {
        await using var f = new Fixture();
        f.Reservation.ArrivalAdjustment = new() { AcceptedAt = Now, RequestedArrivalAt = NewClose.AddMinutes(30) };
        f.Reservation.NoShowDeadlineAt = NewClose.AddMinutes(45);
        await f.Db.SaveChangesAsync();
        var proposal = await f.Propose();
        Assert.False(Assert.Single(proposal.Participants).CanAccept);
        f.AsCustomer();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.RespondAsync(1, new(proposal.Token, true, null), default));
        Assert.Equal(ErrorCode.ScheduleOfferCannotAccept, error.Code);
    }

    [Fact]
    public async Task ExpiredPaymentHoldDoesNotBlockSchedule()
    {
        await using var f = new Fixture();
        f.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        f.Reservation.HoldExpiresAt = Now.AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        var proposal = await f.Propose();
        Assert.Empty(proposal.Participants);
        Assert.True(proposal.CanApply);
    }

    [Fact]
    public async Task ExistingEarlierVacateAgreementDoesNotRequireAnotherConsent()
    {
        await using var f = new Fixture();
        f.Reservation.MustVacateAt = NewClose.AddMinutes(-15);
        await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Propose()).Participants);
    }

    [Fact]
    public async Task TerminalReservationStopsBlockingButIsNotAutomaticallyCancelled()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        f.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
        await f.Db.SaveChangesAsync();
        Assert.Equal("Applied", (await f.Service.ApplyAsync(2, new(proposal.Token), default)).State);
        Assert.Null(f.Reservation.MustVacateAt);
    }

    [Fact]
    public async Task PendingProposalBlocksAffectedNewBookingsAndWithdrawalUnblocks()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        var repo = new RestaurantRepository(f.Db);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => repo.IsRestaurantOpenAsync(2, new(Arrival)));
        Assert.Equal(ErrorCode.ScheduleBookingPaused, error.Code);
        var tables = new TableRepository(f.Db);
        Assert.Empty(await tables.GetReservationTableAvailabilityAsync(2, new(Arrival)));
        await f.Service.WithdrawAsync(2, new(proposal.Token), default);
        Assert.True(await repo.IsRestaurantOpenAsync(2, new(Arrival)));
    }

    [Fact]
    public async Task DirectUpdateCannotBypassConsentForExistingBookings()
    {
        await using var f = new Fixture();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.EnsureDirectChangeAllowedAsync(
            f.Restaurant, ScheduleTerms.Normalize(f.Request().WorkingHours), f.Restaurant.TimeZone, default));
        Assert.Equal(ErrorCode.ScheduleChangeRequiresConsent, error.Code);
        Assert.Equal(TimeOnly.MinValue, f.Restaurant.WorkingHours.First().ClosesAt);
    }

    [Fact]
    public async Task DirectUpdateCannotReplacePendingProposal()
    {
        await using var f = new Fixture();
        await f.Propose();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.EnsureDirectChangeAllowedAsync(
            f.Restaurant, ScheduleTerms.Normalize(f.Request(22).WorkingHours), f.Restaurant.TimeZone, default));
        Assert.Equal(ErrorCode.ScheduleChangeAlreadyPending, error.Code);
    }

    [Fact]
    public async Task NewOpenSessionIsRecheckedAtApplyAndPreventsPartialApplication()
    {
        await using var f = new Fixture();
        var proposal = await f.Propose();
        f.AsCustomer();
        await f.Service.RespondAsync(1, new(proposal.Token, true, null), default);
        f.Db.TableSessions.Add(new TableSession { Restaurant = f.Restaurant, Table = f.Reservation.Table,
            OpenedAt = Now, StatusId = StatusIds.TableSession(TableSessionStatus.Open) });
        await f.Db.SaveChangesAsync();
        f.AsManager();
        var result = await f.Service.ApplyAsync(2, new(proposal.Token), default);
        Assert.Equal("Pending", result.State);
        Assert.Equal(2, result.Participants.Count);
        Assert.Null(f.Reservation.MustVacateAt);
        Assert.Equal(TimeOnly.MinValue, f.Restaurant.WorkingHours.First().ClosesAt);
    }

    [Fact]
    public async Task AppliedAgreementUpdatesAcceptedDelayDisplayWithoutMovingDeadline()
    {
        await using var f = new Fixture();
        f.Reservation.ArrivalAdjustment = new() { AcceptedAt = Now, RequestedArrivalAt = Arrival.AddMinutes(30) };
        f.Reservation.NoShowDeadlineAt = Arrival.AddMinutes(45);
        await f.Db.SaveChangesAsync();
        var proposal = await f.Propose();
        f.AsCustomer();
        await f.Service.RespondAsync(1, new(proposal.Token, true, null), default);
        f.AsManager();
        await f.Service.ApplyAsync(2, new(proposal.Token), default);
        Assert.Equal(NewClose, f.Reservation.ArrivalAdjustment.MustVacateAt);
        Assert.Equal(Arrival.AddMinutes(45), f.Reservation.NoShowDeadlineAt);
    }

    [Fact]
    public async Task ExpiredPaymentCannotRespondToExistingOffer()
    {
        await using var f = new Fixture();
        f.Reservation.StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        f.Reservation.HoldExpiresAt = Now.AddMinutes(10);
        await f.Db.SaveChangesAsync();
        await f.Propose();
        f.Reservation.HoldExpiresAt = Now;
        await f.Db.SaveChangesAsync();
        f.AsCustomer();
        Assert.False((await f.Service.GetCustomerOfferAsync(1, default))!.CanRespond);
    }

    [Fact]
    public async Task ArrivalOutsideNewOpeningRequiresResolutionNotBlindConsent()
    {
        await using var f = new Fixture();
        var request = f.Request();
        foreach (var hour in request.WorkingHours) hour.OpensAt = new(20, 0);
        var proposal = await f.Service.ProposeAsync(2, request, default);
        var participant = Assert.Single(proposal.Participants);
        Assert.Null(participant.ProposedVacateAt);
        Assert.False(participant.CanAccept);
        Assert.False(proposal.CanApply);
    }

    [Fact]
    public async Task InvalidIncompleteHoursCannotCreateProposal()
    {
        await using var f = new Fixture();
        var request = f.Request();
        request.WorkingHours[0] = null!;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ProposeAsync(2, request, default));
        Assert.Equal(ErrorCode.ScheduleInvalidRequest, error.Code);
        Assert.Empty(await f.Db.RestaurantScheduleChanges.ToListAsync());
    }

    [Fact]
    public void OvernightArrivalUsesPreviousServiceDayClosing()
    {
        var current = new[] { new RestaurantWorkingHour { DayOfWeek = DayOfWeek.Friday,
            OpensAt = new(19, 0), ClosesAt = new(2, 0), CloseDayOffset = 1 } };
        var proposed = new[] { new RestaurantWorkingHour { DayOfWeek = DayOfWeek.Friday,
            OpensAt = new(19, 0), ClosesAt = new(1, 30), CloseDayOffset = 1 } };
        var arrival = new DateTime(2026, 10, 2, 21, 0, 0, DateTimeKind.Utc);
        Assert.True(ScheduleTerms.IsAffected(current, proposed, "Asia/Baku", arrival, null, out var end));
        Assert.Equal(arrival.AddMinutes(30), end);
    }
}
