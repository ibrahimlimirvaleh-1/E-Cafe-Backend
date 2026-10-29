using System.Data;
using System.Security.Claims;
using AutoMapper;
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
using ECafe.Application.Services.Table.Concrete;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationArrivalCutoffServiceTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task CreateUsesCurrentCutoffInsideScheduleLockAndCapturesOriginalTerms(int minutesAfterCutoff, bool allowed)
    {
        var f = new Fixture();
        var request = new CreateReservationRequest { TableId = 3, PeopleCount = 2, ReservedAt = new(f.Cutoff.AddMinutes(minutesAfterCutoff)) };
        if (!allowed)
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.CreateReservationAsync(2, request));
            Assert.Equal(ErrorCode.ReservationArrivalCutoffPassed, error.Code);
            Assert.Null(f.Created);
            f.Reservations.Verify(repository => repository.SaveChangesAsync(), Times.Never);
            return;
        }
        var result = await f.Service.CreateReservationAsync(2, request);
        Assert.Equal(f.Cutoff, f.Created!.ReservationArrivalCutoffAt);
        Assert.Equal(new DateTimeOffset(f.Cutoff), result.ReservationArrivalCutoffAt);
        Assert.Equal(f.Cutoff.AddMinutes(15), f.Created.NoShowDeadlineAt);
        foreach (var hour in f.Restaurant.WorkingHours) hour.LastReservationArrivalAt = new TimeOnly(22, 0);
        Assert.Equal(f.Cutoff, f.Created.ReservationArrivalCutoffAt);
        Assert.Equal(f.Cutoff, f.Created.ReservedAt);
    }

    [Fact]
    public async Task NewCutoffIsReadAfterWaitingForTheScheduleLock()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Restaurants.Setup(repository => repository.AcquireScheduleLockAsync(2, It.IsAny<CancellationToken>())).Returns(gate.Task);
        var pending = f.Service.CreateReservationAsync(2, new CreateReservationRequest { TableId = 3, PeopleCount = 2, ReservedAt = new(f.Cutoff) });
        foreach (var hour in f.Restaurant.WorkingHours) hour.LastReservationArrivalAt = new TimeOnly(22, 0);
        gate.SetResult();
        Assert.Equal(ErrorCode.ReservationArrivalCutoffPassed, (await Assert.ThrowsAsync<BusinessRuleException>(() => pending)).Code);
        Assert.Null(f.Created);
    }

    [Fact]
    public async Task AvailabilitySaysRestaurantIsOpenWhenOnlyReservationArrivalCutoffHasPassed()
    {
        var f = new Fixture();
        var manager = new TableManager(new HttpContextAccessor(), Mock.Of<IMapper>(), Mock.Of<IConfiguration>(),
            f.Tables.Object, Mock.Of<IAuditLogService>(), f.Restaurants.Object, f.Deposits.Object);
        var response = await manager.CheckAvailabilityAsync(2, new(f.Cutoff.AddMinutes(5)));
        Assert.True(response.IsRestaurantOpen);
        Assert.False(response.IsReservationArrivalAllowed);
        Assert.False(response.HasAvailableTable);
        Assert.Equal(nameof(ErrorCode.ReservationArrivalCutoffPassed), response.MessageCode);
        Assert.Equal(new DateTimeOffset(f.Cutoff), response.ReservationArrivalCutoffAt);
        Assert.Empty(response.Tables);
    }

    [Fact]
    public async Task UnconfiguredCutoffDoesNotAddAnyThirtyMinuteRestrictionToNewBookings()
    {
        var f = new Fixture();
        foreach (var hour in f.Restaurant.WorkingHours)
        {
            hour.LastReservationArrivalAt = null;
            hour.LastReservationArrivalDayOffset = null;
        }
        var result = await f.Service.CreateReservationAsync(2, new CreateReservationRequest
        {
            TableId = 3, PeopleCount = 2, ReservedAt = new(f.Cutoff.AddMinutes(55))
        });
        Assert.Null(result.ReservationArrivalCutoffAt);
        Assert.Null(f.Created!.ReservationArrivalCutoffAt);
    }

    private sealed class Fixture
    {
        public DateTime Cutoff { get; } = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(19), DateTimeKind.Utc);
        public Restaurant Restaurant { get; } = new()
        {
            Id = 2, IsActive = true, Name = "Test", TimeZone = "Asia/Baku", NoShowGraceMinutes = 15,
            WorkingHours = Enum.GetValues<DayOfWeek>().Select(day => new RestaurantWorkingHour
            {
                DayOfWeek = day, OpensAt = new(9, 0), ClosesAt = TimeOnly.MinValue, CloseDayOffset = 1,
                LastReservationArrivalAt = new(23, 0), LastReservationArrivalDayOffset = 0
            }).ToList()
        };
        public Mock<IRestaurantRepository> Restaurants { get; } = new();
        public Mock<IReservationRepository> Reservations { get; } = new();
        public Mock<ITableRepository> Tables { get; } = new();
        public Mock<IRestaurantDepositService> Deposits { get; } = new();
        public Reservation? Created { get; private set; }
        public ReservationManager Service { get; }

        /// <summary>Yalnız limit və snapshot davranışını təcrid edir; ödəniş, fayl, bildiriş və real DB çağırışları edilmir.</summary>
        public Fixture()
        {
            var table = new Table { Id = 3, RestaurantId = 2, IsActive = true, Capacity = 2 };
            Tables.Setup(repository => repository.GetByIdAsync(3)).ReturnsAsync(table);
            Tables.Setup(repository => repository.GetReservationTableAvailabilityAsync(2, 3, It.IsAny<DateTimeOffset>(), null))
                .ReturnsAsync(new ReservationTableAvailability(table, null));
            Restaurants.Setup(repository => repository.GetByIdAsync(2)).ReturnsAsync(Restaurant);
            Restaurants.Setup(repository => repository.IsRestaurantOpenAsync(2, It.IsAny<DateTimeOffset>())).ReturnsAsync(true);
            Restaurants.Setup(repository => repository.GetReservationArrivalCutoffAsync(2, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int _, DateTimeOffset arrival, CancellationToken _) => RestaurantReservationArrivalPolicy.GetCutoffAt(Restaurant, arrival));
            Restaurants.Setup(repository => repository.HasRestaurantActiveContractAsync(2)).ReturnsAsync(true);
            var contracts = new Mock<IRestaurantContractRepository>();
            contracts.Setup(repository => repository.HasActiveContractAsync(2)).ReturnsAsync(true);
            Reservations.Setup(repository => repository.Add(It.IsAny<Reservation>()))
                .Callback<Reservation>(reservation => { reservation.Id = 11; reservation.Restaurant = Restaurant; Created = reservation; })
                .ReturnsAsync((Reservation reservation) => reservation);
            var staff = new Mock<IUserRestaurantRepository>();
            staff.Setup(repository => repository.GetActiveByRestaurantAndRolesAsync(2, It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync([]);
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(value => value.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "4")], "test")) };
            Service = new ReservationManager(new HttpContextAccessor { HttpContext = http }, Mock.Of<IMapper>(), Mock.Of<IConfiguration>(),
                Tables.Object, Mock.Of<ITableSessionRepository>(), Restaurants.Object, Deposits.Object, contracts.Object,
                Reservations.Object, staff.Object, Mock.Of<INotificationService>(), Mock.Of<IAuditLogService>(), transactions.Object,
                Mock.Of<IReservationPaymentInstructionRepository>(), Mock.Of<IReservationPaymentProofRepository>(), Mock.Of<IFileRepository>(),
                Mock.Of<IFileAccessUrlService>(), Mock.Of<IWorkflowActionService>(), Mock.Of<IPaymentInstructionDetailsProtector>(),
                Mock.Of<ILogger<ReservationManager>>(), Options.Create(new ReservationTimingOptions()));
        }
    }
}
