using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Features.Queries.File;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.File;
using ECafe.Infrastructure.Repositories.LoginAttempt;
using ECafe.Infrastructure.Repositories.Reservation;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task CustomerClosureChecksEveryRealDatabaseBlockerWithoutBlockingOtherCustomers()
    {
        foreach (var blocker in Enum.GetValues<CustomerAccountClosureBlocker>())
        {
            await using var fixture = await Database.CreateAsync();
            await using var db = fixture.Context();
            await SeedClosureActivityAsync(db, blocker);
            var repository = new CustomerAccountClosureRepository(db);
            Assert.Contains(blocker, await repository.GetBlockersAsync(1001, default));
            Assert.Empty(await repository.GetBlockersAsync(9999, default));
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CustomerClosure(db).DeleteAsync(ClosureRequest(), default));
            Assert.Equal(ErrorCode.AccountDeletionBlocked, error.Code);
            Assert.True((await db.Users.SingleAsync(value => value.Id == 1001)).IsActive);
            Assert.False(await db.AuditLogs.AnyAsync());
            Assert.False(await db.UserRefreshTokens.AnyAsync(value => value.RevokedAt != null));
        }
    }

    [StockPostgresFact]
    public async Task CustomerClosureFindsLegacyDebtAfterFirstFiftyHistoryRowsAndPreservesSettledHistory()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            await SeedClosureActivityAsync(db, CustomerAccountClosureBlocker.UnpaidBill);
            for (var id = 1; id <= 51; id++) db.TableSessions.Add(ClosedHistorySession(id));
            await db.SaveChangesAsync();
            Assert.Contains(CustomerAccountClosureBlocker.UnpaidBill, await new CustomerAccountClosureRepository(db).GetBlockersAsync(1001, default));
            db.Payments.Add(new Payment
            {
                Id = 1001, RestaurantId = 1001, TableSessionId = 1001, Amount = 7, CreatedByUserId = 1001,
                PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online),
                PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid),
                PaidAt = DateTime.UtcNow, ProviderName = "synthetic", ProviderRef = "history-payment", BillFingerprint = new string('A', 64)
            });
            await db.SaveChangesAsync();
            Assert.Empty(await new CustomerAccountClosureRepository(db).GetBlockersAsync(1001, default));
            await CustomerClosure(db).DeleteAsync(ClosureRequest(), default);
        }
        await using var read = fixture.Context();
        Assert.Equal(52, await read.TableSessions.Include(value => value.CustomerUser).CountAsync());
        Assert.Equal(7, await read.OrderItems.SumAsync(value => value.LineTotal));
        Assert.Equal(7, await read.Payments.Include(value => value.CreatedByUser).SumAsync(value => value.Amount));
        Assert.Equal(1, await read.Orders.Include(value => value.CustomerUser).CountAsync());
        Assert.Equal("Deleted", (await read.Orders.Include(value => value.CustomerUser).SingleAsync()).CustomerUser!.Name);
    }

    [StockPostgresFact]
    public async Task CustomerClosureProtectsAnUnrequestedRefundUntilItIsSettled()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        db.Tables.Add(new() { Id = 1001, RestaurantId = 1001, TableNo = 1, Capacity = 2, IsActive = true });
        var reservation = ClosureReservation(ReservationStatus.Cancelled);
        reservation.RefundEligible = true;
        db.Reservations.Add(reservation);
        db.Payments.Add(new() { Id = 1001, RestaurantId = 1001, ReservationId = 1001, CreatedByUserId = 1001, Amount = 7,
            PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online),
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid), PaidAt = DateTime.UtcNow,
            ProviderName = "synthetic", ProviderRef = "unrequested-refund" });
        await db.SaveChangesAsync();
        var repository = new CustomerAccountClosureRepository(db);
        Assert.Contains(CustomerAccountClosureBlocker.PendingRefund, await repository.GetBlockersAsync(1001, default));
        Assert.Equal(ErrorCode.AccountDeletionBlocked,
            (await Assert.ThrowsAsync<BusinessRuleException>(() => CustomerClosure(db).DeleteAsync(ClosureRequest(), default))).Code);
        db.ReservationRefunds.Add(new() { ReservationId = 1001, SourcePaymentId = 1001, Amount = 7,
            StatusId = StatusIds.Refund(RefundStatus.Refunded), RefundedAt = DateTime.UtcNow,
            RequestedAt = DateTime.UtcNow, EligibilityReason = "synthetic-settled" });
        (await db.Payments.SingleAsync()).RefundedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Empty(await repository.GetBlockersAsync(1001, default));
        await CustomerClosure(db).DeleteAsync(ClosureRequest(), default);
        Assert.Equal(7, await db.Payments.SumAsync(value => value.Amount));
        Assert.Equal(7, await db.ReservationRefunds.SumAsync(value => value.Amount));
    }

    [StockPostgresFact]
    public async Task CustomerClosureLockDoesNotBlockRestaurantReviewOfAnInactiveCustomersHistory()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        (await db.Users.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var repository = new ReservationRepository(db);
        await repository.AcquireCustomerReservationLockAsync(1001, 1001, requireActiveCustomer: false);
        Assert.Equal(ErrorCode.SessionInvalid, (await Assert.ThrowsAsync<UnauthorizedException>(() =>
            repository.AcquireCustomerReservationLockAsync(1001, 1001))).Code);
    }

    [StockPostgresFact]
    public async Task CustomerClosureRevokesRecoveryPushAndEveryDeviceAndOldCredentialsCannotReturn()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            var expires = DateTime.UtcNow.AddDays(1);
            db.EmailChangeRequests.Add(new() { UserId = 1001, OriginalEmail = "test1001@example.test", NewEmail = "new@example.test", TokenHash = Hash(new string('A', 64)), ExpiresAt = expires });
            db.PasswordResetTokens.Add(new() { UserId = 1001, TokenHash = Hash("reset-test"), ExpiresAt = expires });
            db.UserPasswordSetupTokens.Add(new() { UserId = 1001, TokenHash = Hash("setup-test"), ExpiresAt = expires });
            (await db.Users.SingleAsync()).File = new() { Name = "avatar", Url = "synthetic-avatar.png", Token = "closure-avatar",
                Extension = ".png", FileTypeId = (int)FileTypeCode.UserProfileImage };
            db.MobilePushInstallations.Add(new() { Id = Guid.NewGuid(), UserId = 1001, SessionId = Session,
                ExpoProjectId = Guid.NewGuid(), TokenHash = Hash("push-test"), ProtectedToken = "synthetic-protected", IsActive = true, RegisteredAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await CustomerClosure(db).DeleteAsync(ClosureRequest(), default);
        }
        await using (var read = fixture.Context())
        {
            var user = await read.Users.SingleAsync(value => value.Id == 1001);
            Assert.False(user.IsActive);
            Assert.False(user.IsDeleted);
            Assert.Equal(1, user.SessionVersion);
            Assert.Empty(user.Password);
            Assert.NotNull((await read.EmailChangeRequests.SingleAsync()).RevokedAt);
            Assert.NotNull((await read.PasswordResetTokens.SingleAsync()).RevokedAt);
            Assert.NotNull((await read.UserPasswordSetupTokens.SingleAsync()).UsedAt);
            Assert.False(await read.UserRefreshTokens.AnyAsync(value => value.RevokedAt == null));
            var push = await read.MobilePushInstallations.SingleAsync();
            Assert.False(push.IsActive);
            Assert.Empty(push.ProtectedToken);
            Assert.NotNull(push.DeactivatedAt);
            Assert.Single(await read.AuditLogs.ToListAsync());
            Assert.Null(user.FileId);
            var storage = new Mock<IMinioService>();
            await Assert.ThrowsAsync<NotFoundException>(() => new GetFileQueryHandler(new FileRepository(read), storage.Object)
                .Handle(new() { token = "closure-avatar" }, default));
            storage.Verify(value => value.GetFileAsync(It.IsAny<string>()), Times.Never);
        }
        await using (var db = fixture.Context())
            await Assert.ThrowsAnyAsync<BaseException>(() => Service(db).LoginAsync(new() { Email = "test1001@example.test", Password = "test-password" }));
        await using (var db = fixture.Context())
            await Assert.ThrowsAnyAsync<BaseException>(() => Service(db).RefreshAsync(new() { RefreshToken = OriginalToken }));
        await using (var db = fixture.Context())
            await Assert.ThrowsAnyAsync<BaseException>(() => EmailChanges(db).ConfirmAsync(new() { Token = new string('A', 64) }));
        await using (var db = fixture.Context())
            await Assert.ThrowsAnyAsync<BaseException>(() => ResetService(db).ResetPasswordAsync(new() { Token = "reset-test", Password = "new-account-password" }));
        await using (var db = fixture.Context())
        {
            var newAccount = await Service(db).RegisterAsync(new() { Email = "test1001@example.test", Phone = "0501234567",
                Name = "New", Surname = "Identity", Password = "new-account-password" });
            Assert.NotNull(newAccount.AccessToken);
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.NotEqual(1001, (await db.Users.SingleAsync(value => value.Email == "test1001@example.test")).Id);
        }
    }

    [StockPostgresFact]
    public async Task CustomerClosureCommitsExactlyOnceAcrossSixteenIndependentApiScopes()
    {
        await using var fixture = await Database.CreateAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ =>
        {
            await using var db = fixture.Context();
            return await AttemptAsync(() => CustomerClosure(db).DeleteAsync(ClosureRequest(), default));
        }));
        Assert.Single(results, value => value == null);
        Assert.All(results.Where(value => value != null), value => Assert.Equal(ErrorCode.SessionInvalid, Assert.IsType<UnauthorizedException>(value).Code));
        await using var read = fixture.Context();
        Assert.Equal(1, (await read.Users.SingleAsync()).SessionVersion);
        Assert.Single(await read.AuditLogs.ToListAsync());
    }

    [StockPostgresFact]
    public async Task CustomerClosureRollsBackIdentityTokensAndAuditIfDatabaseWriteFails()
    {
        await using var fixture = await Database.CreateAsync();
        var realtime = new Mock<IUserRealtimeNotifier>();
        await using (var db = fixture.Context(new FailingSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => CustomerClosure(db, realtime.Object).DeleteAsync(ClosureRequest(), default));
        await using var read = fixture.Context();
        var user = await read.Users.SingleAsync();
        Assert.True(user.IsActive);
        Assert.Equal("test1001@example.test", user.Email);
        Assert.Equal(0, user.SessionVersion);
        Assert.False(await read.UserRefreshTokens.AnyAsync(value => value.RevokedAt != null));
        Assert.Empty(await read.AuditLogs.ToListAsync());
        realtime.Verify(value => value.NotifyUserDeactivatedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [StockPostgresFact]
    public async Task CustomerClosureWrongPasswordsPersistAndRespectExistingAccountLockout()
    {
        await using var fixture = await Database.CreateAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var db = fixture.Context();
            var loginAttempts = new LoginAttemptManager(new LoginAttemptRepository(db), Mock.Of<IEmailOutboxService>(),
                Mock.Of<ICriticalEventReporter>(), Mock.Of<IHttpContextAccessor>(), new ConfigurationBuilder().Build());
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CustomerClosure(db, loginAttempts: loginAttempts)
                .DeleteAsync(new() { CurrentPassword = "wrong-password", Confirm = true }, default));
            Assert.Equal(ErrorCode.InvalidCredentials, error.Code);
        }
        await using var read = fixture.Context();
        var security = new LoginAttemptManager(new LoginAttemptRepository(read), Mock.Of<IEmailOutboxService>(),
            Mock.Of<ICriticalEventReporter>(), Mock.Of<IHttpContextAccessor>(), new ConfigurationBuilder().Build());
        var locked = await Assert.ThrowsAsync<ForbiddenException>(() => CustomerClosure(read, loginAttempts: security).DeleteAsync(ClosureRequest(), default));
        Assert.Equal(ErrorCode.AccountTemporarilyLocked, locked.Code);
        Assert.Equal(5, await read.LoginAttempts.CountAsync());
        Assert.True((await read.Users.SingleAsync()).IsActive);
        Assert.Empty(await read.AuditLogs.ToListAsync());
    }

    [StockPostgresFact]
    public async Task CustomerClosureAndNewReservationCannotPassEachOtherInEitherLockOrder()
    {
        foreach (var closureFirst in new[] { true, false })
        {
            await using var fixture = await Database.CreateAsync();
            var barrier = new SaveBarrier();
            await using var firstDb = fixture.Context(barrier);
            await using var secondDb = fixture.Context();
            // Əvvəlki transaction user kilidini əlində saxlayarkən ikinci əməliyyatı gözlədir.
            async Task CreateReservation(ECafeDbContext db)
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await new ReservationRepository(db).AcquireCustomerReservationLockAsync(1001, 1001);
                db.Tables.Add(new() { Id = 1001, RestaurantId = 1001, TableNo = 1, Capacity = 2, IsActive = true });
                db.Reservations.Add(ClosureReservation(ReservationStatus.Confirmed));
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            var first = Task.Run(() => AttemptAsync(() => closureFirst ? CustomerClosure(firstDb).DeleteAsync(ClosureRequest(), default) : CreateReservation(firstDb)));
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var second = Task.Run(() => AttemptAsync(() => closureFirst ? CreateReservation(secondDb) : CustomerClosure(secondDb).DeleteAsync(ClosureRequest(), default)));
            try { await Task.Delay(150); Assert.False(second.IsCompleted); }
            finally { barrier.Release.TrySetResult(); }
            Assert.Null(await first);
            var error = await second;
            if (closureFirst) Assert.Equal(ErrorCode.SessionInvalid, Assert.IsType<UnauthorizedException>(error).Code);
            else Assert.Equal(ErrorCode.AccountDeletionBlocked, Assert.IsType<BusinessRuleException>(error).Code);
            await using var read = fixture.Context();
            Assert.Equal(!closureFirst, (await read.Users.SingleAsync()).IsActive);
            Assert.Equal(closureFirst ? 0 : 1, await read.Reservations.CountAsync());
        }
    }

    [StockPostgresFact]
    public async Task CustomerClosureAndStaffActivationCannotRestoreAClosedAccountInEitherLockOrder()
    {
        foreach (var closureFirst in new[] { true, false })
        {
            await using var fixture = await Database.CreateAsync();
            await using (var setup = fixture.Context())
            {
                (await setup.UserRestaurants.SingleAsync()).RoleId = (int)RoleCode.Waiter;
                await setup.SaveChangesAsync();
            }
            var barrier = new SaveBarrier();
            await using var firstDb = fixture.Context(barrier);
            await using var secondDb = fixture.Context();
            var first = Task.Run(() => AttemptAsync(() => closureFirst ? CustomerClosure(firstDb).DeleteAsync(ClosureRequest(), default) : ProfileService(firstDb).ActivateStaffAsync(1001, 1001)));
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var second = Task.Run(() => AttemptAsync(() => closureFirst ? ProfileService(secondDb).ActivateStaffAsync(1001, 1001) : CustomerClosure(secondDb).DeleteAsync(ClosureRequest(), default)));
            try { await Task.Delay(150); Assert.False(second.IsCompleted); }
            finally { barrier.Release.TrySetResult(); }
            Assert.Null(await first);
            var error = Assert.IsType<BusinessRuleException>(await second);
            Assert.Equal(closureFirst ? ErrorCode.StaffAccountInactive : ErrorCode.AccountDeletionBlocked, error.Code);
            await using var read = fixture.Context();
            Assert.Equal(!closureFirst, (await read.Users.SingleAsync()).IsActive);
            Assert.Equal(!closureFirst, (await read.UserRestaurants.SingleAsync()).IsActive);
        }
    }

    /// <summary>Həqiqi repository və transaction istifadə edir, yalnız Redis və bildiriş kanallarını saxtalaşdırır.</summary>
    private static CustomerAccountDeletionManager CustomerClosure(ECafeDbContext db, IUserRealtimeNotifier? realtime = null, ILoginAttemptService? loginAttempts = null)
    {
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([
            new("userId", "1001"), new(ClaimTypes.Role, "5"), new("sessionId", Session), new("sessionVersion", "0")], "test")) };
        return new(new UserRepository(db), new UserRefreshTokenRepository(db), new CustomerAccountClosureRepository(db),
            new Transactions(db), loginAttempts ?? Mock.Of<ILoginAttemptService>(), Mock.Of<IUserSessionStateCache>(), realtime ?? Mock.Of<IUserRealtimeNotifier>(),
            new BaseRepository<AuditLog>(db), Mock.Of<IHttpContextAccessor>(value => value.HttpContext == http), TimeProvider.System,
            new TranslationBootstrap(), NullLogger<CustomerAccountDeletionManager>.Instance);
    }

    /// <summary>Yalnız test bazasındakı sintetik müştərinin şifrəsi ilə açıq təsdiq qurur.</summary>
    private static CustomerAccountDeletionRequest ClosureRequest() => new() { CurrentPassword = "test-password", Confirm = true };

    /// <summary>Borcsuz, tamamlanmış tarixçə sessiyasını səhifələmə sınağı üçün qurur.</summary>
    private static TableSession ClosedHistorySession(int id) => new() { Id = id, RestaurantId = 1001, TableId = 1001,
        CustomerUserId = 1001, StatusId = StatusIds.TableSession(TableSessionStatus.Closed), OpenedAt = DateTime.UtcNow.AddHours(-2), ClosedAt = DateTime.UtcNow.AddHours(-1) };

    /// <summary>Aktiv və tamamlanmış rezervasiya hallarını eyni sintetik müştəriyə bağlayır.</summary>
    private static Reservation ClosureReservation(ReservationStatus status) => new() { Id = 1001, RestaurantId = 1001, TableId = 1001,
        CustomerUserId = 1001, StatusId = StatusIds.Reservation(status), PeopleCount = 2,
        ReservedAt = DateTime.UtcNow, NoShowDeadlineAt = DateTime.UtcNow.AddHours(1) };

    /// <summary>Hər bloklayıcı halı ayrı real DB-də yaradır; ödəniş və tarixçə əlaqələri istehsal sorğuları ilə yoxlanılır.</summary>
    private static async Task SeedClosureActivityAsync(ECafeDbContext db, CustomerAccountClosureBlocker blocker)
    {
        db.Tables.Add(new() { Id = 1001, RestaurantId = 1001, TableNo = 1, Capacity = 2, IsActive = true });
        switch (blocker)
        {
            case CustomerAccountClosureBlocker.ActiveStaffProfile:
                var assignment = await db.UserRestaurants.SingleAsync();
                assignment.RoleId = (int)RoleCode.Waiter;
                assignment.IsActive = true;
                (await db.Restaurants.SingleAsync()).IsActive = false;
                break;
            case CustomerAccountClosureBlocker.ActiveReservation:
                db.Reservations.Add(ClosureReservation(ReservationStatus.Confirmed));
                break;
            case CustomerAccountClosureBlocker.OpenTableSession:
                var open = ClosedHistorySession(1001);
                open.ClosedAt = null;
                open.StatusId = StatusIds.TableSession(TableSessionStatus.Open);
                db.TableSessions.Add(open);
                break;
            case CustomerAccountClosureBlocker.PendingPayment:
                db.Payments.Add(new() { RestaurantId = 1001, CreatedByUserId = 1001, Amount = 7,
                    PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending),
                    PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online) });
                break;
            case CustomerAccountClosureBlocker.PendingRefund:
                db.Reservations.Add(ClosureReservation(ReservationStatus.Cancelled));
                db.ReservationRefunds.Add(new() { ReservationId = 1001, StatusId = StatusIds.Refund(RefundStatus.Requested), Amount = 7,
                    RequestedAt = DateTime.UtcNow, EligibilityReason = "synthetic" });
                break;
            case CustomerAccountClosureBlocker.WalletBalance:
                db.Wallets.Add(new() { UserId = 1001, Balance = 7 });
                break;
            case CustomerAccountClosureBlocker.UnresolvedOrder:
            case CustomerAccountClosureBlocker.UnpaidBill:
                db.TableSessions.Add(ClosedHistorySession(1001));
                db.Categories.Add(new() { Id = 1001, RestaurantId = 1001, Name = "Test", Slug = "test" });
                db.Items.Add(new() { Id = 1001, RestaurantId = 1001, CategoryId = 1001, Name = "Test", BasePrice = 7,
                    StatusId = StatusIds.Build(StatusType.ItemStatus, (int)ItemStatus.Available) });
                db.Orders.Add(new() { Id = 1001, RestaurantId = 1001, TableId = 1001, TableSessionId = 1001, CustomerUserId = 1001,
                    SourceId = (int)OrderSource.Customer, StatusId = StatusIds.Build(StatusType.Order,
                        blocker == CustomerAccountClosureBlocker.UnpaidBill ? (int)OrderStatus.Served : (int)OrderStatus.Preparing),
                    OpenedAt = DateTime.UtcNow.AddHours(-2), ServedAt = DateTime.UtcNow.AddHours(-1) });
                db.OrderItems.Add(new() { OrderId = 1001, ItemId = 1001, Quantity = 1, NameSnapshot = "Test", UnitPrice = 7, UnitPriceSnapshot = 7, LineTotal = 7 });
                break;
        }
        await db.SaveChangesAsync();
    }
}
