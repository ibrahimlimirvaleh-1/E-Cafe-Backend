using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Table;
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
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task UnpaidSevenAznCannotCompleteInPostgres()
    {
        await using var database = await CompletionDatabaseAsync(0);
        await using var request = database.Context();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CompletionManager(request).CompleteOwnReservationAsync(1001, 1001));
        Assert.Equal(ErrorCode.SessionBillUnpaid, error.Code);
        await using var read = database.Context();
        Assert.Null((await read.TableSessions.SingleAsync()).ClosedAt);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Seated), (await read.Reservations.SingleAsync()).StatusId);
        Assert.Equal(7m, TableSessionBillingPolicy.Calculate((await new TableSessionRepository(read)
            .GetForBillingAsync(1001, false, default))!).RemainingAmount);
        Assert.Empty(await read.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task ConcurrentDepositCoveredCompletionsCreateOnlyOneHistoryInPostgres()
    {
        await using var database = await CompletionDatabaseAsync(7);
        await using var first = database.Context();
        await using var second = database.Context();
        await Task.WhenAll(CompletionManager(first).CompleteOwnReservationAsync(1001, 1001),
            CompletionManager(second).CompleteOwnReservationAsync(1001, 1001)).WaitAsync(TimeSpan.FromSeconds(20));
        await using var read = database.Context();
        var reservation = await read.Reservations.Include(item => item.StatusHistory).SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Completed), reservation.StatusId);
        Assert.Single(reservation.StatusHistory);
        Assert.NotNull((await read.TableSessions.SingleAsync()).ClosedAt);
        Assert.Single(await read.Payments.ToListAsync());
        Assert.Empty(await read.Payments.Where(payment => payment.TableSessionId != null).ToListAsync());
    }

    [StockPostgresFact]
    public async Task NewOrderCommittedUnderTableLockBlocksPreviouslyCoveredCompletionInPostgres()
    {
        await using var database = await CompletionDatabaseAsync(7);
        await using var owner = database.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await new TableRepository(owner).AcquireReservationLockAsync(1001, 1001);
        await using var request = database.Context();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tables = new Mock<ITableRepository>();
        tables.Setup(repository => repository.AcquireReservationLockAsync(1001, 1001, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                waiting.TrySetResult();
                await new TableRepository(request).AcquireReservationLockAsync(1001, 1001);
            });
        var completion = CompletionManager(request, tables.Object).CompleteOwnReservationAsync(1001, 1001);
        try
        {
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(completion.IsCompleted);
            owner.Orders.Add(new Order
            {
                Id = 1003, RestaurantId = 1001, TableId = 1001, TableSessionId = 1001, ReservationId = 1001,
                WaiterUserId = 1001, CustomerUserId = 1003, SourceId = (int)OrderSource.Waiter,
                StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served), ServedAt = DateTime.UtcNow,
                OrderItems = [new OrderItem { Id = 1003, ItemId = 1001, NameSnapshot = "Americano", Quantity = 1,
                    UnitPrice = 7, UnitPriceSnapshot = 7, LineTotal = 7 }]
            });
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            await Record.ExceptionAsync(() => completion);
            throw;
        }
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => completion.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal(ErrorCode.SessionBillUnpaid, error.Code);
        await using var read = database.Context();
        var bill = TableSessionBillingPolicy.Calculate((await new TableSessionRepository(read).GetForBillingAsync(1001, false, default))!);
        Assert.Equal(14m, bill.TotalAmount);
        Assert.Equal(7m, bill.DepositCreditAmount);
        Assert.Equal(7m, bill.RemainingAmount);
        Assert.Null((await read.TableSessions.SingleAsync()).ClosedAt);
        Assert.Empty(await read.Set<ReservationStatusHistory>().ToListAsync());
    }

    /// <summary>Yalnız GUID-li lokal test bazasında 7 AZN təqdim edilmiş hesab və istəyə uyğun təsdiqlənmiş depozit qurur.</summary>
    private static async Task<Database> CompletionDatabaseAsync(int deposit)
    {
        var database = await Database.CreateAsync(1);
        try
        {
            await using var db = database.Context();
            db.Users.Add(new User { Id = 1003, Name = "Customer", Surname = "Test", Email = "bill@example.test",
                Phone = "1003", Password = "test", IsActive = true, RoleId = (int)RoleCode.Customer });
            db.Reservations.Add(new Reservation { Id = 1001, RestaurantId = 1001, TableId = 1001,
                CustomerUserId = 1003, WaiterUserId = 1001, PeopleCount = 2, DepositAmount = deposit,
                ReservedAt = DateTime.UtcNow.AddHours(-1), NoShowDeadlineAt = DateTime.UtcNow,
                StatusId = StatusIds.Reservation(ReservationStatus.Seated) });
            db.TableSessions.Add(new TableSession { Id = 1001, RestaurantId = 1001, TableId = 1001,
                ReservationId = 1001, CustomerUserId = 1003, WaiterUserId = 1001, OpenedAt = DateTime.UtcNow,
                StatusId = StatusIds.TableSession(TableSessionStatus.Open) });
            var order = await db.Orders.Include(item => item.OrderItems).SingleAsync(item => item.Id == 1001);
            order.TableSessionId = 1001; order.ReservationId = 1001; order.CustomerUserId = 1003;
            order.StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Served); order.ServedAt = DateTime.UtcNow;
            var line = order.OrderItems.Single();
            line.Quantity = 1; line.UnitPrice = 7; line.UnitPriceSnapshot = 7; line.LineTotal = 7;
            if (deposit > 0) db.Payments.Add(new Payment { Id = 1001, RestaurantId = 1001, ReservationId = 1001,
                CreatedByUserId = 1003, Amount = deposit, CurrencyCode = "AZN", ProviderName = "simulation",
                ProviderRef = "confirmed-deposit", IsTest = true, PaidAt = DateTime.UtcNow,
                PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online),
                PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) });
            await db.SaveChangesAsync();
            return database;
        }
        catch { await database.DisposeAsync(); throw; }
    }

    /// <summary>Real repository və PostgreSQL transaction/kilidləri ilə məsul ofisiantın bağlanış xidmətini qurur; xarici bildiriş/auditi əvəzləyir.</summary>
    private static ReservationManager CompletionManager(ECafeDbContext db, ITableRepository? tables = null)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", "1001"), new Claim("restaurantId", "1001"),
            new Claim(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())], "test")) };
        return new ReservationManager(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http),
            Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), tables ?? new TableRepository(db),
            new TableSessionRepository(db), Mock.Of<IRestaurantRepository>(), Mock.Of<IRestaurantDepositService>(),
            Mock.Of<IRestaurantContractRepository>(), new ReservationRepository(db), new UserRestaurantRepository(db),
            Mock.Of<INotificationService>(), Mock.Of<IAuditLogService>(), new Transactions(db),
            Mock.Of<IReservationPaymentInstructionRepository>(), Mock.Of<IReservationPaymentProofRepository>(),
            Mock.Of<IFileRepository>(), Mock.Of<IFileAccessUrlService>(), Mock.Of<IWorkflowActionService>(),
            Mock.Of<IPaymentInstructionDetailsProtector>(), Mock.Of<ILogger<ReservationManager>>(),
            Options.Create(new ReservationTimingOptions()));
    }
}
