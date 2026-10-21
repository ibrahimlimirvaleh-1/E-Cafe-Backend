using System.Data;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Common.Audit;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.InventoryMovement.Concrete;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.InventoryMovement;
using ECafe.Infrastructure.Repositories.InventoryMovementType;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.Recipe;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.Unit;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;
using Xunit;

namespace ECafe.Tests;

public sealed class StockPostgresFactAttribute : FactAttribute
{
    /// <summary>Real DB sınaqlarını yalnız ayrıca lokal test bağlantısı veriləndə işə salır; adi unit test heç bir bazaya qoşulmur.</summary>
    public StockPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECAFE_STOCK_TEST_CONNECTION")))
            Skip = "Set ECAFE_STOCK_TEST_CONNECTION to an isolated local PostgreSQL cluster.";
    }
}

public sealed class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task PreparingConsumesRecipeOnceAndReadyOrRetryNeverConsumesAgain()
    {
        await using var f = await Database.CreateAsync(1);
        await using (var db = f.Context())
        {
            var service = Service(db);
            var first = await service.StartPreparingAsync(1001, 1001);
            var again = await service.StartPreparingAsync(1001, 1001);
            await service.MarkReadyAsync(1001, 1001);
            await service.StartPreparingAsync(1001, 1001);
            Assert.Equal(first.PreparingAt, again.PreparingAt);
        }
        await using var read = f.Context();
        Assert.Equal(0.5m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        var movement = Assert.Single(await read.InventoryMovements.ToListAsync());
        Assert.Equal(-0.5m, movement.QuantityChange);
        Assert.Equal(1001, movement.RelatedOrderId);
        Assert.Equal(1001, movement.RelatedOrderItemId);
        Assert.Equal(1, movement.UnitId);
    }

    [StockPostgresFact]
    public async Task SameOrderParallelRequestsHaveOneConsumptionAndOnePreparationAudit()
    {
        await using var f = await Database.CreateAsync(1);
        await using var a = f.Context();
        await using var b = f.Context();
        var audit = new Mock<IAuditLogService>();
        var results = await Task.WhenAll(Service(a, audit).StartPreparingAsync(1001, 1001),
            Service(b, audit).StartPreparingAsync(1001, 1001)).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(results[0].PreparingAt, results[1].PreparingAt);
        Assert.Single(audit.Invocations, call => Equals(call.Arguments[1], AuditActions.OrderPreparationStarted));
        await using var read = f.Context();
        Assert.Equal(0.5m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await read.InventoryMovements.ToListAsync());
    }

    [StockPostgresFact]
    public async Task DifferentTablesCompeteForLastStockWithoutOverselling()
    {
        await using var f = await Database.CreateAsync(0.75m);
        await using var a = f.Context();
        await using var b = f.Context();
        var errors = await Task.WhenAll(TryStartAsync(Service(a), 1001), TryStartAsync(Service(b), 1002))
            .WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(errors, error => error == null);
        Assert.Equal(ErrorCode.KitchenOrderStockInsufficient, Assert.Single(errors.OfType<BusinessRuleException>()).Code);
        await using var read = f.Context();
        Assert.Equal(0.25m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await read.InventoryMovements.ToListAsync());
        Assert.Equal(1, await read.Orders.CountAsync(order => order.StatusId == 2003));
        Assert.Equal(1, await read.Orders.CountAsync(order => order.StatusId == 2002));
    }

    [StockPostgresFact]
    public async Task MissingRecipeAndInsufficientStockDoNotChangeStatusOrInventory()
    {
        await using var f = await Database.CreateAsync(0.1m);
        await using (var db = f.Context())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).StartPreparingAsync(1001, 1001));
            Assert.Equal(ErrorCode.KitchenOrderStockInsufficient, error.Code);
        }
        await using (var db = f.Context())
        {
            (await db.Recipes.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        await using (var db = f.Context())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).StartPreparingAsync(1001, 1001));
            Assert.Equal(ErrorCode.KitchenOrderRecipeInvalid, error.Code);
        }
        await using var read = f.Context();
        Assert.Equal(0.1m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await read.InventoryMovements.ToListAsync());
        Assert.All(await read.Orders.ToListAsync(), order => { Assert.Equal(2002, order.StatusId); Assert.Null(order.PreparingAt); });
    }

    [StockPostgresFact]
    public async Task AuditFailureRollsBackStatusStockAndMovementsTogether()
    {
        await using var f = await Database.CreateAsync(1);
        await using (var db = f.Context())
        {
            var audit = new Mock<IAuditLogService>();
            audit.Setup(service => service.RecordRestaurantActionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(),
                It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException("Audit unavailable"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, audit).StartPreparingAsync(1001, 1001));
        }
        await using var read = f.Context();
        Assert.Equal(1m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await read.InventoryMovements.ToListAsync());
        Assert.Null((await read.Orders.SingleAsync(order => order.Id == 1001)).PreparingAt);
    }

    [StockPostgresFact]
    public async Task DatabaseUniqueGuardRejectsDuplicateConsumptionEvenIfMovementWasSoftDeleted()
    {
        await using var f = await Database.CreateAsync(1);
        await using (var db = f.Context()) await Service(db).StartPreparingAsync(1001, 1001);
        await using (var db = f.Context())
        {
            var movement = await db.InventoryMovements.SingleAsync();
            movement.IsDeleted = true;
            await db.SaveChangesAsync();
            db.InventoryMovements.Add(new InventoryMovement { RestaurantId = 1001, InventoryItemId = 1001,
                RelatedOrderId = 1001, RelatedOrderItemId = 1001, UnitId = 1, MovementTypeId = 4, QuantityChange = -0.5m });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23505", Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
    }

    [StockPostgresFact]
    public async Task OneMissingIngredientPreventsAllIngredientDeductions()
    {
        await using var f = await Database.CreateAsync(1);
        await using (var db = f.Context())
        {
            db.InventoryItems.Add(new InventoryItem { Id = 1002, RestaurantId = 1001, Name = "Sugar", UnitId = 1, QuantityOnHand = 0.1m, IsActive = true });
            db.Recipes.Add(new Recipe { Id = 1002, RestaurantId = 1001, ItemId = 1001, InventoryItemId = 1002, UnitId = 2, Quantity = 250, IsActive = true });
            await db.SaveChangesAsync();
        }
        await using (var db = f.Context())
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).StartPreparingAsync(1001, 1001));
        await using var read = f.Context();
        Assert.Equal(1m, (await read.InventoryItems.SingleAsync(item => item.Id == 1001)).QuantityOnHand);
        Assert.Equal(0.1m, (await read.InventoryItems.SingleAsync(item => item.Id == 1002)).QuantityOnHand);
        Assert.Empty(await read.InventoryMovements.ToListAsync());
    }

    [StockPostgresFact]
    public async Task ManualDecreaseAndKitchenConsumptionShareTheSameStockLock()
    {
        await using var f = await Database.CreateAsync(0.75m);
        await using var kitchen = f.Context();
        await using var manual = f.Context();
        var results = await Task.WhenAll(TryStartAsync(Service(kitchen), 1001), TryManualDecreaseAsync(manual))
            .WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(results, error => error == null);
        Assert.Single(results.OfType<BusinessRuleException>());
        await using var read = f.Context();
        Assert.Equal(0.25m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await read.InventoryMovements.ToListAsync());
    }

    [StockPostgresFact]
    public async Task DeletingAnIngredientDoesNotRestoreAStaleQuantityAfterConsumption()
    {
        await using var f = await Database.CreateAsync(1);
        await using var metadata = f.Context();
        await metadata.InventoryItems.SingleAsync();
        await using (var kitchen = f.Context()) await Service(kitchen).StartPreparingAsync(1001, 1001);
        var restaurants = new Mock<ECafe.Application.Repositories.Restaurant.IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetRestaurantInfoAsync(1001)).ReturnsAsync(new Restaurant { Id = 1001 });
        var manager = new ECafe.Application.Services.InventoryItem.Concrete.InventoryItemManager(Accessor(),
            Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), restaurants.Object,
            new InventoryItemRepository(metadata), new UnitRepository(metadata));
        await manager.DeleteAsync(1001, 1001);
        await using var read = f.Context();
        var item = await read.InventoryItems.IgnoreQueryFilters().SingleAsync();
        Assert.True(item.IsDeleted);
        Assert.Equal(0.5m, item.QuantityOnHand);
    }

    /// <summary>Əl ilə azalma üçün yalnız inventar əməliyyatını çağırır; real repository kilidi mətbəx transaction-ı ilə yarışır.</summary>
    private static async Task<Exception?> TryManualDecreaseAsync(ECafeDbContext db)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<InventoryMovement>(It.IsAny<CreateInventoryMovementRequest>())).Returns(new InventoryMovement { MovementTypeId = 3 });
        mapper.Setup(m => m.Map<InventoryMovementResponse>(It.IsAny<InventoryMovement>())).Returns(new InventoryMovementResponse());
        var restaurants = new Mock<ECafe.Application.Repositories.Restaurant.IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetRestaurantInfoAsync(1001)).ReturnsAsync(new Restaurant { Id = 1001 });
        var manager = new InventoryMovementManager(Accessor(), mapper.Object, new ConfigurationBuilder().Build(),
            new InventoryMovementRepository(db), restaurants.Object, new InventoryItemRepository(db),
            new InventoryMovementTypeRepository(db), new UnitRepository(db), Mock.Of<IInventoryLowStockNotifier>(), new Transactions(db));
        try
        {
            await manager.CreateAsync(1001, 1001, new CreateInventoryMovementRequest { Quantity = 0.5m, UnitId = 1, MovementTypeId = 3 });
            return null;
        }
        catch (BusinessRuleException error) { return error; }
    }

    /// <summary>Eyni real PostgreSQL kontekstini paylaşan repository və transaction-larla mətbəx xidmətini qurur.</summary>
    private static KitchenOrderManager Service(ECafeDbContext db, Mock<IAuditLogService>? audit = null)
    {
        var stock = new OrderStockManager(new RecipeRepository(db), new InventoryItemRepository(db),
            new InventoryMovementRepository(db), Mock.Of<IInventoryLowStockNotifier>());
        return new KitchenOrderManager(Accessor(), Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), new OrderRepository(db),
            Mock.Of<ITableSessionService>(), new TableRepository(db), new UserRestaurantRepository(db), new Transactions(db),
            Mock.Of<INotificationService>(), audit?.Object ?? Mock.Of<IAuditLogService>(), TimeProvider.System, stock);
    }

    /// <summary>Həmin restoranın aktiv mətbəx işçisinin sorğu kontekstini test üçün qurur.</summary>
    private static IHttpContextAccessor Accessor()
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", "1002"), new Claim("restaurantId", "1001"), new Claim(ClaimTypes.Role, "6")], "test")) };
        return Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http);
    }

    /// <summary>Paralel sorğuların biznes nəticələrini müqayisə etmək üçün uğuru null, uğursuzluğu isə xəta kimi saxlayır.</summary>
    private static async Task<Exception?> TryStartAsync(KitchenOrderManager service, int orderId)
    {
        try { await service.StartPreparingAsync(1001, orderId); return null; }
        catch (BusinessRuleException error) { return error; }
    }

    private sealed class Transactions(ECafeDbContext db) : IApplicationDbTransactionFactory
    {
        /// <summary>Testdə real DB transaction-ını başladır.</summary>
        public Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken token = default)
            => BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        /// <summary>İstehsaldakı izolyasiya səviyyəsini testdə də qoruyur.</summary>
        public async Task<IApplicationDbTransaction> BeginTransactionAsync(IsolationLevel level, CancellationToken token = default)
            => new Transaction(await db.Database.BeginTransactionAsync(level, token));
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IApplicationDbTransaction
    {
        /// <summary>Testdə bütün dəyişiklikləri vahid commit ilə təsdiqləyir.</summary>
        public Task CommitAsync(CancellationToken token = default) => transaction.CommitAsync(token);
        /// <summary>Test transaction-ını geri qaytarır.</summary>
        public Task RollbackAsync(CancellationToken token = default) => transaction.RollbackAsync(token);
        /// <summary>Commit olmayan sınaqda DB-nin rollback davranışını saxlayır.</summary>
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name = "ecafe_stock_test_" + Guid.NewGuid().ToString("N");
        private string _connection = "";

        /// <summary>Yalnız ayrıca loopback test klasterinə icazə verir; mövcud layihə konfiqurasiyasını oxumur.</summary>
        private Database()
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ECAFE_STOCK_TEST_CONNECTION"));
            if (builder.Host is not ("127.0.0.1" or "localhost") || builder.Port == 5432 || builder.Database != "postgres")
                throw new InvalidOperationException("Use a dedicated loopback test cluster, non-default port and postgres admin database.");
            builder.Pooling = false;
            _admin = builder.ConnectionString;
            builder.Database = _name;
            _connection = builder.ConnectionString;
        }

        /// <summary>Hər oxunuş/yazı üçün müstəqil bağlantı ilə real yarış şəraiti yaradır.</summary>
        public ECafeDbContext Context() => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql(_connection, options => options.CommandTimeout(10)).Options);

        /// <summary>Yalnız bu testin yaratdığı unikal bazada model və reseptli iki ayrı masa sifarişi qurur.</summary>
        public static async Task<Database> CreateAsync(decimal quantity)
        {
            var database = new Database();
            await using var admin = new NpgsqlConnection(database._admin);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database._name}", admin).ExecuteNonQueryAsync();
            try
            {
                await using var db = database.Context();
                await db.Database.EnsureCreatedAsync();
                db.Restaurants.Add(new Restaurant { Id = 1001, Name = "Stock test", Location = "Baku", Phone = "1001", IsActive = true });
                db.Categories.Add(new Category { Id = 1001, RestaurantId = 1001, Name = "Meals", Slug = "stock-meals", IsActive = true });
                db.Items.Add(new Item { Id = 1001, RestaurantId = 1001, CategoryId = 1001, Name = "Meal", IsActive = true, IsAvailable = true, StatusId = 5001, BasePrice = 3 });
                db.InventoryItems.Add(new InventoryItem { Id = 1001, RestaurantId = 1001, Name = "Flour", UnitId = 1, QuantityOnHand = quantity, IsActive = true });
                db.Recipes.Add(new Recipe { Id = 1001, RestaurantId = 1001, ItemId = 1001, InventoryItemId = 1001, UnitId = 2, Quantity = 250, IsActive = true });
                for (var i = 1001; i <= 1002; i++)
                {
                    db.Tables.Add(new Table { Id = i, RestaurantId = 1001, TableNo = i - 1000, Capacity = 2, IsActive = true });
                    db.Users.Add(new User { Id = i, Name = "Test", Surname = i.ToString(), Email = $"stock{i}@example.test", Phone = i.ToString(), Password = "test", IsActive = true, RoleId = i == 1001 ? 4 : 6 });
                    db.UserRestaurants.Add(new UserRestaurant { Id = i, UserId = i, RestaurantId = 1001, RoleId = i == 1001 ? 4 : 6, IsActive = true });
                    db.Orders.Add(new Order { Id = i, RestaurantId = 1001, TableId = i, WaiterUserId = 1001,
                        StatusId = 2002, SourceId = 2, OpenedAt = DateTime.UtcNow, SentToKitchenAt = DateTime.UtcNow,
                        AcceptedAt = DateTime.UtcNow, AcceptedByUserId = 1002,
                        OrderItems = [new OrderItem { Id = i, ItemId = 1001, NameSnapshot = "Meal", Quantity = 2, UnitPrice = 3, UnitPriceSnapshot = 3, LineTotal = 6 }] });
                }
                await db.SaveChangesAsync();
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        /// <summary>Yalnız bu fixture-in yaratdığı GUID-li test bazasını silir; istifadəçi və server bazalarına toxunmur.</summary>
        public async ValueTask DisposeAsync()
        {
            await using var admin = new NpgsqlConnection(_admin);
            await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
