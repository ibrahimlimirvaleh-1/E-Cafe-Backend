using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Orders;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Item;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task CustomerParallelCreatesAndApprovalsAreSingleAndLeaveStockUntouched()
    {
        await using var f = await Database.CreateAsync(1);
        await SeedCustomerSessionAsync(f);
        var key = Guid.NewGuid();
        await using var first = f.Context(true);
        await using var second = f.Context(true);
        var results = await Task.WhenAll(CustomerSubmitter(first).CreateAsync(1001, key, CustomerRequest()),
            CustomerSubmitter(second).CreateAsync(1001, key, CustomerRequest())).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(results, result => !result.IsReplay);
        Assert.Single(results, result => result.IsReplay);
        Assert.Equal(results[0].Order.Id, results[1].Order.Id);
        Assert.All(results, result => Assert.Equal(2009, result.Order.StatusId));
        await using (var read = f.Context())
        {
            Assert.Null((await read.Orders.SingleAsync(order => order.SourceId == 1)).SentToKitchenAt);
            Assert.Equal(1, await read.OutboxEvents.CountAsync(entry => entry.EventType == OutboxEventTypes.OrderRealtimeRequested));
        }
        await using var waiter1 = f.Context(true);
        await using var waiter2 = f.Context(true);
        var sent = await Task.WhenAll(CustomerApprover(waiter1).SendAsync(1001, 1001, results[0].Order.Id),
            CustomerApprover(waiter2).SendAsync(1001, 1001, results[0].Order.Id)).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(sent[0].SentToKitchenAt, sent[1].SentToKitchenAt);
        Assert.All(sent, order => Assert.Equal(2001, order.StatusId));
        await using var final = f.Context();
        Assert.Equal(2, await final.OutboxEvents.CountAsync(entry => entry.EventType == OutboxEventTypes.OrderRealtimeRequested));
        Assert.Equal(1m, (await final.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await final.InventoryMovements.ToListAsync());
        Assert.Empty(await final.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task CustomerOrderRollbackRemovesOrderAndRealtimeEvent()
    {
        await using var f = await Database.CreateAsync(1);
        await SeedCustomerSessionAsync(f);
        await using var db = f.Context(true);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(service => service.RecordRestaurantActionAsync(It.IsAny<int>(), AuditActions.OrderCreated,
                It.IsAny<object>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Simulated audit persistence failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CustomerSubmitter(db, audit.Object)
            .CreateAsync(1001, Guid.NewGuid(), CustomerRequest()));
        await using var read = f.Context();
        Assert.False(await read.Orders.AnyAsync(order => order.SourceId == 1));
        Assert.Empty(await read.OutboxEvents.ToListAsync());
    }

    [StockPostgresFact]
    public async Task CustomerOrderCanCompleteTheExistingKitchenAndServingFlow()
    {
        await using var f = await Database.CreateAsync(1);
        await SeedCustomerSessionAsync(f);
        int orderId;
        await using (var customer = f.Context())
            orderId = (await CustomerSubmitter(customer).CreateAsync(1001, Guid.NewGuid(), CustomerRequest())).Order.Id;
        await using (var waiter = f.Context()) await CustomerApprover(waiter).SendAsync(1001, 1001, orderId);
        await using (var kitchen = f.Context()) await Service(kitchen).AcceptAsync(1001, orderId);
        await using (var kitchen = f.Context()) await Service(kitchen).StartPreparingAsync(1001, orderId);
        await using (var kitchen = f.Context()) await Service(kitchen).MarkReadyAsync(1001, orderId);
        await using (var waiter = f.Context())
            Assert.Equal(2005, (await CustomerSubmitter(waiter, asWaiter: true).MarkServedAsync(1001, 1001, orderId)).StatusId);
        await using var read = f.Context();
        Assert.Equal(0.5m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await read.InventoryMovements.ToListAsync());
        Assert.NotNull((await read.Orders.SingleAsync(order => order.Id == orderId)).ServedAt);
    }

    [StockPostgresFact]
    public async Task CancellationRacingWithApprovalNeverResurrectsTheOrder()
    {
        await using var f = await Database.CreateAsync(1);
        await SeedCustomerSessionAsync(f);
        int orderId;
        await using (var customer = f.Context())
            orderId = (await CustomerSubmitter(customer).CreateAsync(1001, Guid.NewGuid(), CustomerRequest())).Order.Id;
        await using var approve = f.Context();
        await using var cancel = f.Context();
        // Ləğv hər iki kilid sıralamasında mümkündür; göndərmə yalnız ləğvdən əvvəl kilid alarsa uğurludur.
        async Task ApproveAsync()
        {
            try { await CustomerApprover(approve).SendAsync(1001, 1001, orderId); }
            catch (ECafe.Domain.Exceptions.BusinessRuleException) { }
        }
        await Task.WhenAll(ApproveAsync(), CancellationService(cancel).CancelForWaiterAsync(1001, 1001, orderId,
            new CancelOrderRequest("Customer changed their mind"))).WaitAsync(TimeSpan.FromSeconds(20));
        await using var read = f.Context();
        Assert.Equal(2007, (await read.Orders.SingleAsync(order => order.Id == orderId)).StatusId);
        Assert.Equal(1m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Empty(await read.InventoryMovements.ToListAsync());
    }

    /// <summary>Real bazada aktiv müştəri, ofisiant və müqavilə ilə özünə aid açıq masa sessiyası yaradır.</summary>
    private static async Task SeedCustomerSessionAsync(Database database)
    {
        await using var db = database.Context();
        db.Users.Add(new User { Id = 1003, Name = "Customer", Surname = "Test", Email = "customer@example.test",
            Phone = "1003", Password = "test", IsActive = true, RoleId = 5 });
        db.RestaurantContracts.Add(new RestaurantContract { RestaurantId = 1001, ContractNumber = "customer-test",
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
            StatusId = StatusIds.Contract(ContractStatus.Active) });
        db.TableSessions.Add(new TableSession { Id = 1001, RestaurantId = 1001, TableId = 1001,
            CustomerUserId = 1003, WaiterUserId = 1001, OpenedAt = DateTime.UtcNow,
            StatusId = StatusIds.TableSession(TableSessionStatus.Open) });
        await db.SaveChangesAsync();
    }

    /// <summary>Real kilid və transaction ilə müştəri yaratma xidmətini qurur; istehsal bazasına və xarici sistemlərə müraciət etmir.</summary>
    private static OrderManager CustomerSubmitter(ECafeDbContext db, IAuditLogService? audit = null, bool asWaiter = false)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", asWaiter ? "1001" : "1003"), new Claim("restaurantId", "1001"),
            new Claim(ClaimTypes.Role, asWaiter ? "4" : "5")], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http);
        var config = new ConfigurationBuilder().Build();
        var sessions = new TableSessionRepository(db);
        var access = new TableSessionManager(accessor, Mock.Of<IMapper>(), config, sessions, new UserRestaurantRepository(db));
        var limits = Options.Create(new OrderCreationOptions());
        return new OrderManager(accessor, Mock.Of<IMapper>(), config, new OrderRepository(db), new ItemRepository(db),
            sessions, access, new TableRepository(db), new Transactions(db), audit ?? Mock.Of<IAuditLogService>(),
            new CreateWaiterOrderValidator(limits), limits, TimeProvider.System);
    }

    /// <summary>İki ayrı bağlantının eyni sifariş təsdiqində real masa kilidini paylaşmasını yoxlayan ofisiant xidmətini qurur.</summary>
    private static KitchenOrderManager CustomerApprover(ECafeDbContext db)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", "1001"), new Claim("restaurantId", "1001"), new Claim(ClaimTypes.Role, "4")], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http);
        var config = new ConfigurationBuilder().Build();
        var assignments = new UserRestaurantRepository(db);
        var access = new TableSessionManager(accessor, Mock.Of<IMapper>(), config, new TableSessionRepository(db), assignments);
        return new KitchenOrderManager(accessor, Mock.Of<IMapper>(), config, new OrderRepository(db), access,
            new TableRepository(db), assignments, new Transactions(db), Mock.Of<INotificationService>(),
            Mock.Of<IAuditLogService>(), TimeProvider.System, Mock.Of<IOrderStockService>());
    }

    /// <summary>Reseptli məhsul üçün real stok sınağında istifadə olunan iki ədədlik müştəri sifarişini yaradır.</summary>
    private static CreateCustomerOrderRequest CustomerRequest() => new()
        { Items = [new() { ItemId = 1001, Quantity = 2, ExpectedUnitPrice = 3 }] };
}
