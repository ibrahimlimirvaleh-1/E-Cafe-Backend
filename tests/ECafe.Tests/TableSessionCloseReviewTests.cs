using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.PaymentInstructionDetails.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class TableSessionCloseReviewTests
{
    [Theory]
    [InlineData("2026-10-15T19:00:00Z", "2026-10-15T22:00:00Z", false)]
    [InlineData("2026-10-15T22:00:00Z", "2026-10-15T22:00:00Z", true)]
    [InlineData("2026-10-16T08:00:00Z", "2026-10-15T22:00:00Z", true)]
    public void OvernightBakuBusinessDayDoesNotEndAtMidnight(string now, string due, bool expected)
    {
        var session = SessionWithHours("Asia/Baku", Utc("2026-10-15T09:00:00Z"), new TimeOnly(9, 0), new TimeOnly(2, 0), 1);
        Assert.Equal(Utc(due), TableSessionCloseReviewPolicy.GetDueAt(session));
        Assert.Equal(expected, TableSessionCloseReviewPolicy.RequiresReview(session, Utc(now)));
    }

    [Fact]
    public void EarlyMorningSeatingBelongsToPreviousBusinessDay()
    {
        var session = SessionWithHours("Asia/Baku", Utc("2026-10-15T21:00:00Z"), new TimeOnly(9, 0), new TimeOnly(2, 0), 1);
        Assert.Equal(Utc("2026-10-15T22:00:00Z"), TableSessionCloseReviewPolicy.GetDueAt(session));
    }

    [Fact]
    public void DaylightSavingUsesClosingTimeOffsetNotOpeningOffset()
    {
        var session = SessionWithHours("America/New_York", Utc("2026-03-08T04:00:00Z"), new TimeOnly(20, 0), new TimeOnly(4, 0), 1);
        Assert.Equal(Utc("2026-03-08T08:00:00Z"), TableSessionCloseReviewPolicy.GetDueAt(session));
    }

    [Fact]
    public void MissingScheduleRequiresReviewButNeverCompletesSession()
    {
        var session = new TableSession { StatusId = StatusIds.TableSession(TableSessionStatus.Open), OpenedAt = Utc("2026-10-15T09:00:00Z") };
        Assert.True(TableSessionCloseReviewPolicy.RequiresReview(session, Utc("2026-10-16T09:00:00Z")));
        Assert.Null(session.ClosedAt);
        Assert.Equal(StatusIds.TableSession(TableSessionStatus.Open), session.StatusId);
        session.ClosedAt = Utc("2026-10-16T09:00:00Z");
        Assert.False(TableSessionCloseReviewPolicy.RequiresReview(session, Utc("2026-10-16T09:00:00Z")));
    }

    [Fact]
    public async Task LegacyReviewAppearsInOwnListAndManagerQueueWithoutChangingStatus()
    {
        await using var f = await Fixture.CreateAsync();
        var own = await new TableSessionManager(f.Accessor, Mock.Of<IMapper>(), f.Configuration,
            new TableSessionRepository(f.Db, f.Clock), new UserRestaurantRepository(f.Db)).GetMyOpenSessionAsync(1, 10);
        Assert.True(own.RequiresCloseReview);
        f.Actor(3, RoleCode.Manager);
        var queue = await f.Review.GetReviewsAsync(1, new PaginationFilter());
        Assert.Equal(new[] { 10, 11 }, queue.Items.Select(item => item.Id));
        Assert.True(queue.Items.All(item => item.RequiresCloseReview));
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Seated), (await f.Db.Reservations.SingleAsync()).StatusId);
        Assert.All(await f.Db.TableSessions.Where(s => s.Id == 10 || s.Id == 11).ToListAsync(), s => Assert.Null(s.ClosedAt));
    }

    [Fact]
    public async Task NotificationRunsOnceAndTargetsOnlyResponsibleWaiterAndRestaurantManagers()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Equal(2, await f.Review.NotifyDueReviewsAsync(100));
        Assert.Equal(0, await f.Review.NotifyDueReviewsAsync(100));
        var mine = f.Sent.Where(n => n.RelatedEntityId == 10).ToList();
        Assert.Equal(new[] { 1, 3, 4 }, mine.Select(n => n.UserId).Order());
        Assert.All(mine, n => Assert.Equal((int)NotificationType.TableSessionCloseReviewRequired, n.TypeId));
        Assert.DoesNotContain(f.Sent, n => n.UserId == 5 || n.RestaurantId != 1);
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 12)).CloseReviewNotifiedAt);
    }

    [Fact]
    public async Task ManagerContinuationIsAuditedAndDoesNotExtendBookingOrTouchOrders()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Review.NotifyDueReviewsAsync(100);
        f.Actor(3, RoleCode.Manager);
        var body = new ContinueTableServiceRequest(f.Clock.GetUtcNow().AddHours(1), "Müştəri hələ xidmətdədir");
        var response = await f.Review.ContinueServiceAsync(1, 10, body);
        Assert.False(response.RequiresCloseReview);
        Assert.Equal(body.ReviewAgainAt, response.CloseReviewDueAt);
        var session = await f.Db.TableSessions.SingleAsync(s => s.Id == 10);
        Assert.Null(session.ClosedAt);
        Assert.Null(session.CloseReviewNotifiedAt);
        Assert.Equal(3, session.CloseReviewConfirmedByUserId);
        var reservation = await f.Db.Reservations.Include(r => r.StatusHistory).SingleAsync();
        Assert.Equal(Utc("2026-10-15T18:00:00Z"), reservation.MustVacateAt);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Seated), reservation.StatusId);
        Assert.Single(reservation.StatusHistory);
        await f.Review.ContinueServiceAsync(1, 10, body);
        Assert.Single(reservation.StatusHistory);
        Assert.DoesNotContain((await f.Review.GetReviewsAsync(1, new PaginationFilter())).Items, item => item.Id == 10);
        Assert.Equal(0, await f.Review.NotifyDueReviewsAsync(100));
        f.Clock.Now = body.ReviewAgainAt;
        Assert.Equal(1, await f.Review.NotifyDueReviewsAsync(100));
        Assert.Equal(0, await f.Review.NotifyDueReviewsAsync(100));
    }

    [Theory]
    [InlineData(0, "ok")]
    [InlineData(-1, "ok")]
    [InlineData(25, "ok")]
    [InlineData(1, " ")]
    public async Task InvalidContinuationCannotChangeSession(int hours, string reason)
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(3, RoleCode.Manager);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Review.ContinueServiceAsync(1, 10,
            new(f.Clock.GetUtcNow().AddHours(hours), reason)));
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).CloseReviewConfirmedAt);
    }

    [Theory]
    [InlineData(1, RoleCode.Waiter)]
    [InlineData(5, RoleCode.Customer)]
    [InlineData(2, RoleCode.Manager)] // Token says manager, active DB assignment is waiter.
    public async Task UnauthorizedStaffCannotManageReview(int actor, RoleCode tokenRole)
    {
        await using var f = await Fixture.CreateAsync(); f.Actor(actor, tokenRole);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Review.GetReviewsAsync(1, new PaginationFilter()));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Review.ContinueServiceAsync(1, 10,
            new(f.Clock.GetUtcNow().AddHours(1), "test")));
    }

    [Fact]
    public async Task ClosedAndForeignSessionsCannotBeContinued()
    {
        await using var f = await Fixture.CreateAsync(); f.Actor(3, RoleCode.Manager);
        foreach (var id in new[] { 13, 14, 999 })
            await Assert.ThrowsAsync<NotFoundException>(() => f.Review.ContinueServiceAsync(1, id,
                new(f.Clock.GetUtcNow().AddHours(1), "test")));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Review.GetReviewsAsync(2, new PaginationFilter()));
    }

    [Fact]
    public async Task WorkerRechecksClosureAfterTakingTableLock()
    {
        await using var f = await Fixture.CreateAsync();
        f.BeforeLock = async () =>
        {
            var session = await f.Db.TableSessions.SingleAsync(s => s.Id == 10);
            session.ClosedAt = f.Clock.GetUtcNow().UtcDateTime;
            session.StatusId = StatusIds.TableSession(TableSessionStatus.Closed);
            await f.Db.SaveChangesAsync(); f.BeforeLock = null;
        };
        await f.Review.NotifyDueReviewsAsync(100);
        Assert.DoesNotContain(f.Sent, n => n.RelatedEntityId == 10);
    }

    [Fact]
    public async Task SecondWorkerCannotNotifyAgainWhenFirstWorkerWinsTheLock()
    {
        await using var f = await Fixture.CreateAsync();
        await using var other = f.Fork();
        f.BeforeLock = async () => { await other.Review.NotifyDueReviewsAsync(100); f.BeforeLock = null; };
        Assert.Equal(0, await f.Review.NotifyDueReviewsAsync(100));
        Assert.Empty(f.Sent);
        Assert.Equal(6, other.Sent.Count);
    }

    [Fact]
    public async Task ContinuationRechecksActiveRoleAfterWaitingForLock()
    {
        await using var f = await Fixture.CreateAsync(); f.Actor(3, RoleCode.Manager);
        f.BeforeLock = async () =>
        {
            var assignment = await f.Db.UserRestaurants.SingleAsync(a => a.UserId == 3);
            assignment.IsActive = false; await f.Db.SaveChangesAsync(); f.BeforeLock = null;
        };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Review.ContinueServiceAsync(1, 10,
            new(f.Clock.GetUtcNow().AddHours(1), "test")));
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).CloseReviewConfirmedAt);
    }

    [Fact]
    public async Task CancelledWorkerDoesNotSendOrMutateAnything()
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Review.NotifyDueReviewsAsync(100, new CancellationToken(true)));
        Assert.Empty(f.Sent);
    }

    [Fact]
    public async Task WaiterCanCompleteOnlyOwnSessionAndRetriesDoNotDuplicateHistory()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(2, RoleCode.Waiter);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Reservations.CompleteOwnReservationAsync(1, 75));
        f.Actor(1, RoleCode.Waiter);
        await f.Reservations.CompleteOwnReservationAsync(1, 75);
        await f.Reservations.CompleteOwnReservationAsync(1, 75);
        var reservation = await f.Db.Reservations.Include(r => r.StatusHistory).SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Completed), reservation.StatusId);
        Assert.Single(reservation.StatusHistory);
        Assert.Equal(1, reservation.CompletedByUserId);
        Assert.NotNull((await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).ClosedAt);
        Assert.Single(f.Sent, n => n.TypeId == (int)NotificationType.ReservationCompleted);
    }

    [Theory]
    [InlineData(false, OrderStatus.Served, false)]
    [InlineData(true, OrderStatus.Served, false)]
    [InlineData(false, OrderStatus.Preparing, true)]
    [InlineData(true, OrderStatus.Ready, true)]
    public async Task BothCompletionPathsRejectUnpaidOrUnservedBill(bool manager, OrderStatus status, bool paid)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Orders.Add(new Order { Id = 99, RestaurantId = 1, TableId = 1, TableSessionId = 10,
            StatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.Order, (int)status),
            ServedAt = status == OrderStatus.Served ? DateTime.UtcNow : null,
            OrderItems = [new OrderItem { Id = 99, ItemId = 1, NameSnapshot = "Tea", Quantity = 2,
                UnitPriceSnapshot = 5, UnitPrice = 5, LineTotal = 10 }] });
        if (paid) f.Db.Payments.Add(new Payment { Id = 99, RestaurantId = 1, TableSessionId = 10, Amount = 10,
            CurrencyCode = "AZN", ProviderName = "simulation", ProviderRef = "test", IsTest = true, PaidAt = DateTime.UtcNow,
            PaymentStatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.PaymentStatus, (int)PaymentStatus.Paid) });
        await f.Db.SaveChangesAsync();
        f.Actor(manager ? 3 : 1, manager ? RoleCode.Manager : RoleCode.Waiter);
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager
            ? f.Reservations.CompleteReservationAsync(1, 75) : f.Reservations.CompleteOwnReservationAsync(1, 75));
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).ClosedAt);
        Assert.Null((await f.Db.Reservations.SingleAsync()).CompletedAt);
        Assert.DoesNotContain(f.Sent, notification => notification.TypeId == (int)NotificationType.ReservationCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingPaymentBlocksBothCompletionPathsEvenWhenBalanceIsZero(bool manager)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Payments.Add(new Payment { Id = 99, RestaurantId = 1, TableSessionId = 10, Amount = 10,
            PaymentStatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.PaymentStatus, (int)PaymentStatus.Pending) });
        await f.Db.SaveChangesAsync();
        f.Actor(manager ? 3 : 1, manager ? RoleCode.Manager : RoleCode.Waiter);
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager
            ? f.Reservations.CompleteReservationAsync(1, 75) : f.Reservations.CompleteOwnReservationAsync(1, 75));
        Assert.Null((await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).ClosedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothCompletionPathsClosePaidServedBillOnce(bool manager)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.Orders.Add(new Order { Id = 99, RestaurantId = 1, TableId = 1, TableSessionId = 10,
            StatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.Order, (int)OrderStatus.Served), ServedAt = DateTime.UtcNow,
            OrderItems = [new OrderItem { Id = 99, ItemId = 1, NameSnapshot = "Tea", Quantity = 2,
                UnitPriceSnapshot = 5, UnitPrice = 5, LineTotal = 10 }] });
        f.Db.Payments.Add(new Payment { Id = 99, RestaurantId = 1, TableSessionId = 10, Amount = 10,
            CurrencyCode = "AZN", ProviderName = "simulation", ProviderRef = "test", IsTest = true, PaidAt = DateTime.UtcNow,
            PaymentStatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.PaymentStatus, (int)PaymentStatus.Paid) });
        await f.Db.SaveChangesAsync();
        f.Actor(manager ? 3 : 1, manager ? RoleCode.Manager : RoleCode.Waiter);
        if (manager) await f.Reservations.CompleteReservationAsync(1, 75);
        else await f.Reservations.CompleteOwnReservationAsync(1, 75);
        var closedAt = (await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).ClosedAt;
        Assert.NotNull(closedAt);
        if (manager) await f.Reservations.CompleteReservationAsync(1, 75);
        else await f.Reservations.CompleteOwnReservationAsync(1, 75);
        Assert.Equal(closedAt, (await f.Db.TableSessions.SingleAsync(s => s.Id == 10)).ClosedAt);
        Assert.Single(f.Sent, notification => notification.TypeId == (int)NotificationType.ReservationCompleted);
        Assert.Single((await f.Db.Reservations.Include(r => r.StatusHistory).SingleAsync()).StatusHistory);
    }

    [Fact]
    public async Task StaleRoleAndWrongCompletionEndpointAreRejected()
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Reservations.CompleteReservationAsync(1, 75));
        f.Actor(3, RoleCode.Waiter);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Reservations.CompleteOwnReservationAsync(1, 75));
        f.Actor(3, RoleCode.Manager);
        await f.Reservations.CompleteReservationAsync(1, 75);
        Assert.Equal(3, (await f.Db.Reservations.SingleAsync()).CompletedByUserId);
    }

    /// <summary>Testin UTC tarixlərini sistem saatından asılı olmadan çevirir.</summary>
    private static DateTime Utc(string value) => DateTimeOffset.Parse(value).UtcDateTime;

    /// <summary>Hər gün üçün eyni saatlı test restoranı və açıq sessiya yaradır.</summary>
    private static TableSession SessionWithHours(string zone, DateTime opened, TimeOnly start, TimeOnly end, int dayOffset)
        => new() { OpenedAt = opened, StatusId = StatusIds.TableSession(TableSessionStatus.Open), Restaurant = new()
        { TimeZone = zone, WorkingHours = Enum.GetValues<DayOfWeek>().Select(day => new RestaurantWorkingHour
            { DayOfWeek = day, OpensAt = start, ClosesAt = end, CloseDayOffset = dayOffset }).ToList() } };

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(Utc("2026-10-16T08:00:00Z"));
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; }
        public DefaultHttpContext Http { get; } = new();
        public IHttpContextAccessor Accessor { get; }
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
        public Clock Clock { get; } = new();
        public List<CreateNotificationRequest> Sent { get; } = [];
        public Func<Task>? BeforeLock { get; set; }
        public TableSessionCloseReviewManager Review { get; }
        public ReservationManager Reservations { get; }

        /// <summary>Həqiqi EF yaddaş repository-lərini qurur; bildiriş, kilid, audit və transaction xarici sərhədləri əvəzlənir.</summary>
        private Fixture(ECafeDbContext db)
        {
            Db = db;
            Accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == Http);
            Actor(1, RoleCode.Waiter);
            var tables = new Mock<ITableRepository>();
            tables.Setup(t => t.AcquireReservationLockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { if (BeforeLock != null) await BeforeLock(); });
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
            var notifications = new Mock<INotificationService>();
            notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>())).Callback<CreateNotificationRequest>(Sent.Add).Returns(Task.CompletedTask);
            var audit = new Mock<IAuditLogService>();
            var mapper = Mock.Of<IMapper>(); var staff = new UserRestaurantRepository(db); var sessions = new TableSessionRepository(db);
            Review = new(Accessor, mapper, Configuration, sessions, tables.Object, staff, transactions.Object,
                notifications.Object, audit.Object, Options.Create(new TableSessionReviewOptions()), Clock);
            Reservations = new(Accessor, mapper, Configuration, tables.Object, sessions,
                Mock.Of<IRestaurantRepository>(), Mock.Of<IRestaurantDepositService>(), Mock.Of<IRestaurantContractRepository>(),
                new ReservationRepository(db), staff, notifications.Object, audit.Object, transactions.Object,
                Mock.Of<IReservationPaymentInstructionRepository>(), Mock.Of<IReservationPaymentProofRepository>(), Mock.Of<IFileRepository>(),
                Mock.Of<IFileAccessUrlService>(), Mock.Of<IWorkflowActionService>(), Mock.Of<IPaymentInstructionDetailsProtector>(),
                Mock.Of<ILogger<ReservationManager>>(), Options.Create(new ReservationTimingOptions()));
        }

        /// <summary>Aktiv restoran kontekstini değiştirir; DB rolu ayrıca qalaraq köhnə token ssenarilərini sınamağa imkan verir.</summary>
        public void Actor(int id, RoleCode role) => Http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", id.ToString()), new Claim("restaurantId", "1"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));

        /// <summary>Eyni test bazası üzərində ayrı sorğu konteksti açır; kilid gözləyən worker-in təkrar oxunuşunu sınayır.</summary>
        public Fixture Fork() => new(new ECafeDbContext((DbContextOptions<ECafeDbContext>)Db.GetService<IDbContextOptions>()));

        /// <summary>İki restoran, məsul və başqa ofisiant, menecer, sahibkar, köhnə/future/bağlı sessiyaları hazırlayır.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var restaurant = SessionWithHours("Asia/Baku", default, new TimeOnly(9, 0), new TimeOnly(2, 0), 1).Restaurant;
            restaurant.Id = 1; restaurant.Name = "One"; restaurant.Phone = "1"; restaurant.Location = "Baku"; restaurant.IsActive = true;
            foreach (var role in Enum.GetValues<RoleCode>())
                db.Roles.Add(new Role { Id = (int)role, Name = role.ToString() });
            foreach (var status in Enum.GetValues<ReservationStatus>())
                db.Statuses.Add(new Status { Id = StatusIds.Reservation(status), Name = status.ToString(), StatusTypeId = (int)ECafe.Domain.Enums.StatusType.Reservation });
            db.Restaurants.AddRange(restaurant, new Restaurant { Id = 2, Name = "Two", Phone = "2", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true });
            for (var id = 1; id <= 5; id++)
                db.Users.Add(new User { Id = id, Name = "User", Surname = id.ToString(), Email = $"u{id}@example.test", Phone = id.ToString(), Password = "test", IsActive = true });
            for (var id = 1; id <= 4; id++)
                db.UserRestaurants.Add(new UserRestaurant { Id = id, RestaurantId = 1, UserId = id, IsActive = true,
                    RoleId = (int)(id <= 2 ? RoleCode.Waiter : id == 3 ? RoleCode.Manager : RoleCode.Owner) });
            for (var id = 1; id <= 5; id++)
                db.Tables.Add(new Table { Id = id, RestaurantId = id == 5 ? 2 : 1, TableNo = id, Name = $"Masa-{id}", Capacity = 4, IsActive = true });
            db.Reservations.Add(new Reservation { Id = 75, RestaurantId = 1, TableId = 1, CustomerUserId = 5, WaiterUserId = 1,
                ReservedAt = Utc("2026-10-15T09:00:00Z"), NoShowDeadlineAt = Utc("2026-10-15T09:15:00Z"),
                MustVacateAt = Utc("2026-10-15T18:00:00Z"), PeopleCount = 2, StatusId = StatusIds.Reservation(ReservationStatus.Seated) });
            for (var id = 10; id <= 14; id++)
                db.TableSessions.Add(new TableSession { Id = id, RestaurantId = id == 14 ? 2 : 1, TableId = id - 9,
                    WaiterUserId = id == 11 ? 2 : 1, ReservationId = id == 10 ? 75 : null, CustomerUserId = 5,
                    OpenedAt = Utc(id == 12 ? "2026-10-16T07:00:00Z" : "2026-10-15T09:00:00Z"),
                    CloseReviewDueAt = id == 14 ? Utc("2026-10-15T22:00:00Z") : null,
                    StatusId = StatusIds.TableSession(id == 13 ? TableSessionStatus.Closed : TableSessionStatus.Open),
                    ClosedAt = id == 13 ? Utc("2026-10-15T18:00:00Z") : null });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return new(db);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
