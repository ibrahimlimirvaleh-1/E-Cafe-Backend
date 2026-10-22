using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Dashboard;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ECafe.Tests;

public sealed class DashboardPostgresFactAttribute : FactAttribute
{
    /// <summary>Real DB sınağına yalnız ayrıca lokal test klasteri verildikdə icazə verir.</summary>
    public DashboardPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECAFE_DASHBOARD_TEST_CONNECTION")))
            Skip = "Set ECAFE_DASHBOARD_TEST_CONNECTION to a dedicated loopback PostgreSQL test cluster.";
    }
}

public sealed class DashboardPostgresTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

    [DashboardPostgresFact]
    public async Task RealSqlPreservesRestaurantBoundaryPaymentModesAndDepositAccounting()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var access = await repository.ResolveAccessAsync(scope, default);
        Assert.Equal(1001, Assert.Single(access!.Restaurants).Id);
        Assert.Null(await repository.ResolveAccessAsync(scope with { RestaurantId = 1002 }, default));
        var totals = await repository.GetTotalsAsync(scope, period.StartUtc, period.EndUtc, default);
        Assert.Equal(1, totals.OrderCount);
        Assert.Equal(50, totals.OrderAmount);
        Assert.Equal(50, totals.CollectedAmount);
        Assert.Equal(10, totals.DepositCollectedAmount);
        Assert.Equal(40, totals.SessionCollectedAmount);
        Assert.Equal(10, totals.RefundedAmount);
        var test = await repository.GetTotalsAsync(scope with { IsTest = true }, period.StartUtc, period.EndUtc, default);
        Assert.Equal(200, test.CollectedAmount);
        Assert.Equal(0, test.RefundedAmount);
        var branch = Assert.Single((await repository.GetBranchesAsync(scope, period, Now, 1, default)).Rows);
        Assert.Equal(50, branch.OrderAmount);
        Assert.Equal(50, branch.CollectedAmount);
        Assert.Equal(10, branch.RefundedAmount);
        Assert.Equal(2, branch.ReservationCount);
        var chart = DashboardChartMapper.Map(RoleCode.Owner, period, await repository.GetChartAsync(scope, period, default));
        Assert.Equal(50, chart.Points.Sum(point => point.Value));
        Assert.Equal(50, chart.Points.Single(point => point.BucketStartUtc == period.StartUtc).Value);
        var kathmandu = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Kathmandu", new(Now));
        var shifted = DashboardChartMapper.Map(RoleCode.Owner, kathmandu, await repository.GetChartAsync(scope, kathmandu, default));
        Assert.Equal(50, shifted.Points.Sum(point => point.Value));
    }

    [DashboardPostgresFact]
    public async Task RealSqlBuildsPlatformAndManagerViewsWithNoTrackingOrWrites()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var platform = new DashboardScope(1000, RoleCode.SuperAdmin, null, false);
        Assert.NotNull(await repository.ResolveAccessAsync(platform, default));
        var platformChart = await repository.GetChartAsync(platform, period, default);
        Assert.Equal(2, platformChart.Sum(point => point.Value));
        var platformLive = await repository.GetLiveAsync(platform, Now, period.StartUtc, period.EndUtc, default);
        Assert.Equal(2, platformLive.ActiveRestaurants);
        Assert.Equal(1, platformLive.ActiveContracts);
        Assert.Equal(1, platformLive.ExpiringContracts);
        Assert.Equal(1, platformLive.LowStock);
        var manager = new DashboardScope(1002, RoleCode.Manager, 1001, false);
        Assert.NotNull(await repository.ResolveAccessAsync(manager, default));
        var live = await repository.GetLiveAsync(manager, Now, period.StartUtc, period.EndUtc, default);
        Assert.Equal(1, live.OpenSessions);
        Assert.Equal(1, live.ReadyOrders);
        var sessions = await repository.GetSessionsAsync(manager, 1, default);
        var row = DashboardSessionMapper.Map(Assert.Single(sessions.Rows));
        Assert.False(row.RequiresReview);
        Assert.Equal(50, row.OrderAmount);
        Assert.Equal(0, row.RemainingAmount);
        Assert.Equal("Masa 1", row.TableName);
        Assert.Equal(1, row.ReadyCount);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty((await repository.GetSessionsAsync(manager, 2, default)).Rows);
    }

    [DashboardPostgresFact]
    public async Task SoftDeletedAssignmentsAndLinesAreExcludedByActualSqlFilters()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var line = await db.OrderItems.SingleAsync(item => item.Id == 1001);
        line.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Equal(0, (await repository.GetTotalsAsync(scope, period.StartUtc, period.EndUtc, default)).OrderAmount);
        Assert.Equal(0, (await repository.GetChartAsync(scope, period, default)).Sum(point => point.Value));
        var assignment = await db.UserRestaurants.SingleAsync(item => item.UserId == 1001);
        assignment.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Null(await repository.ResolveAccessAsync(scope, default));
        Assert.Empty((await repository.GetBranchesAsync(scope, period, Now, 1, default)).Rows);
    }

    [DashboardPostgresFact]
    public async Task BranchContractSelectionKeepsContractlessRowsAndPrefersCurrentActiveOverFutureContract()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        db.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 1002, RestaurantId = 1001, ContractNumber = "DASH-FUTURE", StatusId = 6007,
            StartDate = Now.AddDays(10), EndDate = Now.AddDays(90), PaymentPolicyId = 1
        });
        await db.SaveChangesAsync();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1000, RoleCode.SuperAdmin, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));

        var page = await repository.GetBranchesAsync(scope, period, Now, 1, default);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Rows.Count);
        var own = page.Rows.Single(row => row.RestaurantId == 1001);
        Assert.Equal(1001, own.ContractId);
        Assert.Equal(6003, own.ContractStatusId);
        Assert.Equal(Now.AddDays(8), own.ContractEndsAt);
        var other = page.Rows.Single(row => row.RestaurantId == 1002);
        Assert.Null(other.ContractId);
        Assert.Null(other.ContractStatusId);
        Assert.Null(other.ContractEndsAt);

        (await db.RestaurantContracts.SingleAsync(contract => contract.Id == 1001)).IsDeleted = true;
        await db.SaveChangesAsync();
        var after = await repository.GetBranchesAsync(scope, period, Now, 1, default);
        Assert.Equal(1002, after.Rows.Single(row => row.RestaurantId == 1001).ContractId);
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name = "ecafe_dashboard_test_" + Guid.NewGuid().ToString("N");
        private readonly string _connection;

        /// <summary>Layihənin connection string-ini oxumur; yalnız default olmayan portda ayrıca loopback klasterini qəbul edir.</summary>
        private Database()
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ECAFE_DASHBOARD_TEST_CONNECTION"));
            if (builder.Host is not ("127.0.0.1" or "localhost") || builder.Port == 5432 || builder.Database != "postgres")
                throw new InvalidOperationException("Use a dedicated loopback test cluster and postgres database on a non-default port.");
            builder.Pooling = false; _admin = builder.ConnectionString;
            builder.Database = _name; _connection = builder.ConnectionString;
        }

        /// <summary>Yalnız fixture-in yaratdığı unikal bazaya tracking-siz oxuma sınaqları üçün kontekst qurur.</summary>
        public ECafeDbContext Context() => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql(_connection, options => options.CommandTimeout(20)).Options);

        /// <summary>Real model, iki restoran, canlı rollar, depozit, yekun hesab və test/refund nümunələri yaradır.</summary>
        public static async Task<Database> CreateAsync()
        {
            var f = new Database();
            await using var admin = new NpgsqlConnection(f._admin); await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {f._name}", admin).ExecuteNonQueryAsync();
            try
            {
                await using var db = f.Context(); await db.Database.EnsureCreatedAsync();
                for (var i = 1000; i <= 1004; i++) db.Users.Add(new User { Id = i, Name = "Dashboard", Surname = i.ToString(),
                    Email = $"dash{i}@example.test", Phone = i.ToString(), Password = "test", IsActive = true, RoleId = i - 999 });
                db.Restaurants.AddRange(new Restaurant { Id = 1001, Name = "Own", Location = "Baku", Phone = "1001", IsActive = true },
                    new Restaurant { Id = 1002, Name = "Other", Location = "Baku", Phone = "1002", IsActive = true });
                db.UserRestaurants.AddRange(new UserRestaurant { Id = 1001, UserId = 1001, RestaurantId = 1001, RoleId = 2, IsActive = true },
                    new UserRestaurant { Id = 1002, UserId = 1002, RestaurantId = 1001, RoleId = 3, IsActive = true });
                db.Tables.AddRange(new Table { Id = 1001, RestaurantId = 1001, TableNo = 1, Name = "Masa 1", Capacity = 2, IsActive = true },
                    new Table { Id = 1002, RestaurantId = 1002, TableNo = 1, Name = "Other table", Capacity = 2, IsActive = true });
                db.Categories.Add(new Category { Id = 1001, RestaurantId = 1001, Name = "Meals", Slug = "dashboard-meals", IsActive = true });
                db.Items.Add(new Item { Id = 1001, RestaurantId = 1001, CategoryId = 1001, Name = "Meal", StatusId = 5001, IsActive = true, IsAvailable = true });
                var reservation = new Reservation { Id = 1001, RestaurantId = 1001, TableId = 1001, CustomerUserId = 1004,
                    PeopleCount = 2, ReservedAt = Now, NoShowDeadlineAt = Now.AddMinutes(15), DepositAmount = 10, StatusId = 1003, SeatedAt = Now };
                db.Reservations.Add(reservation);
                db.Reservations.Add(new Reservation { Id = 1002, RestaurantId = 1001, TableId = 1001, CustomerUserId = 1004,
                    PeopleCount = 2, ReservedAt = Now.AddDays(-1), NoShowDeadlineAt = Now, DepositAmount = 10, StatusId = 1005 });
                db.Reservations.Add(new Reservation { Id = 1003, RestaurantId = 1001, TableId = 1001, CustomerUserId = 1004,
                    PeopleCount = 2, ReservedAt = Now, NoShowDeadlineAt = Now, DepositAmount = 0, StatusId = 1002 });
                db.TableSessions.Add(new TableSession { Id = 1001, RestaurantId = 1001, TableId = 1001, WaiterUserId = 1003,
                    CustomerUserId = 1004, ReservationId = 1001, StatusId = 7001, OpenedAt = Now });
                db.Orders.Add(new Order { Id = 1001, RestaurantId = 1001, TableId = 1001, TableSessionId = 1001, StatusId = 2004,
                    SourceId = 2, OpenedAt = new DateTime(2026, 10, 21, 20, 15, 0, DateTimeKind.Utc), SentToKitchenAt = Now.AddMinutes(-20), ReadyAt = Now,
                    OrderItems = [new OrderItem { Id = 1001, ItemId = 1001, NameSnapshot = "Meal", Quantity = 2, UnitPriceSnapshot = 25, UnitPrice = 25, LineTotal = 50 },
                        new OrderItem { Id = 1002, ItemId = 1001, NameSnapshot = "Deleted", Quantity = 1, UnitPriceSnapshot = 900, LineTotal = 900, IsDeleted = true }] });
                db.Orders.Add(new Order { Id = 1002, RestaurantId = 1001, TableId = 1001, StatusId = 2007, SourceId = 2, OpenedAt = Now,
                    OrderItems = [new OrderItem { Id = 1003, ItemId = 1001, NameSnapshot = "Cancelled", Quantity = 1, UnitPriceSnapshot = 900, LineTotal = 900 }] });
                db.Orders.Add(new Order { Id = 1003, RestaurantId = 1002, TableId = 1002, StatusId = 2001, SourceId = 2, OpenedAt = Now,
                    OrderItems = [new OrderItem { Id = 1004, ItemId = 1001, NameSnapshot = "Other restaurant", Quantity = 1, UnitPriceSnapshot = 900, LineTotal = 900 }] });
                db.Payments.AddRange(Payment(1001, 10, false, 1001), Payment(1002, 40, false, null, 1001),
                    Payment(1003, 200, true, 1003), Payment(1004, 10, false, 1002, paidAt: Now.AddDays(-1)),
                    Payment(1005, 999, false, null, restaurantId: 1002));
                var refunded = db.Payments.Local.Single(payment => payment.Id == 1004);
                refunded.PaymentStatusId = 3005; refunded.RefundedAt = Now;
                db.ReservationRefunds.Add(new ReservationRefund { Id = 1001, ReservationId = 1002, SourcePaymentId = 1004,
                    StatusId = 8005, Amount = 10, CurrencyCode = "AZN", RequestedAt = Now, RefundedAt = Now, EligibilityReason = "Test" });
                db.InventoryItems.Add(new InventoryItem { Id = 1001, RestaurantId = 1001, UnitId = 1, Name = "Flour", QuantityOnHand = 1, LowStockThreshold = 5, IsActive = true });
                db.RestaurantContracts.Add(new RestaurantContract { Id = 1001, RestaurantId = 1001, ContractNumber = "DASH-1", StatusId = 6003,
                    StartDate = Now.AddDays(-30), EndDate = Now.AddDays(8), PaymentPolicyId = 1 });
                await db.SaveChangesAsync();
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        /// <summary>Hər nümunəyə fərqli provayder istinadı verərək test və real pul hadisələrini ayrıca yaradır.</summary>
        private static Payment Payment(int id, decimal amount, bool test, int? reservationId, int? sessionId = null,
            DateTime? paidAt = null, int restaurantId = 1001)
            => new() { Id = id, RestaurantId = restaurantId, ReservationId = reservationId, TableSessionId = sessionId,
                BillFingerprint = sessionId.HasValue ? "test-fingerprint" : null, Amount = amount, IsTest = test, CurrencyCode = "AZN",
                ProviderName = "DashboardTest", ProviderRef = $"dash-{id}", PaymentStatusId = 3002, PaymentMethodId = 4002, PaidAt = paidAt ?? Now };

        /// <summary>Yalnız bu fixture-in GUID-li test bazasını silir; server və istifadəçi bazalarına toxunmur.</summary>
        public async ValueTask DisposeAsync()
        {
            await using var admin = new NpgsqlConnection(_admin); await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
