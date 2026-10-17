using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Orders;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Item;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class WaiterOrderCreationTests
{
    [Fact]
    public async Task CreateBindsServerContextAndStoresPriceAndNameSnapshotsWithoutKitchenSideEffects()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.CreateAsync(Request(1, 2));
        Assert.False(result.IsReplay);
        Assert.Equal(1, result.Order.TableSessionId);
        Assert.Equal(1, result.Order.TableId);
        Assert.Equal(1, result.Order.RestaurantId);
        Assert.Equal(1, result.Order.WaiterUserId);
        Assert.Equal(10, result.Order.ReservationId);
        Assert.Equal((int)OrderSource.Waiter, result.Order.SourceId);
        Assert.Equal(StatusIds.Build(ECafe.Domain.Enums.StatusType.Order, (int)OrderStatus.Created), result.Order.StatusId);
        Assert.Null(result.Order.SentToKitchenAt);
        Assert.Equal(7m, result.Order.Subtotal);
        var line = Assert.Single(result.Order.Items);
        Assert.Equal("Tea", line.Name);
        Assert.Equal(3.50m, line.UnitPrice);
        Assert.Equal(7m, line.LineTotal);
        var saved = await fixture.Context.Orders.SingleAsync();
        Assert.Equal(3, saved.CustomerUserId);
        Assert.Equal(64, saved.CreationRequestHash!.Length);
        Assert.Null(saved.AcceptedAt);
        Assert.Null(saved.ScheduledKitchenTime);
        Assert.Empty(await fixture.Context.InventoryMovements.ToListAsync());
        Assert.Empty(await fixture.Context.Payments.ToListAsync());
        fixture.Audit.Verify(audit => audit.RecordRestaurantActionAsync(1, AuditActions.OrderCreated,
            It.IsAny<object>(), AuditEntityTypes.Order, saved.Id, null), Times.Once);
    }

    [Fact]
    public async Task ClientCannotOverrideServerPriceActorStatusTableOrSource()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = JsonSerializer.Deserialize<CreateWaiterOrderRequest>("""
            {"WaiterUserId":2,"TableId":2,"RestaurantId":2,"CustomerUserId":2,"SourceId":1,"StatusId":999,
             "Items":[{"ItemId":1,"Quantity":2,"UnitPrice":0,"LineTotal":0}]}
            """)!;
        var result = await fixture.CreateAsync(request);
        Assert.Equal(7m, result.Order.Subtotal);
        Assert.Equal(1, result.Order.TableId);
        Assert.Equal(1, result.Order.WaiterUserId);
        Assert.Equal((int)OrderSource.Waiter, result.Order.SourceId);
    }

    [Fact]
    public async Task SameKeyAndIntentReplaysOriginalOrderEvenWhenMenuChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var key = Guid.NewGuid();
        var request = new CreateWaiterOrderRequest
        {
            Note = "  No sugar  ", Items = [new() { ItemId = 1, Quantity = 1, ExpectedUnitPrice = 3.50m }]
        };
        var first = await fixture.CreateAsync(request, key);
        var product = await fixture.Context.Set<Item>().SingleAsync(item => item.Id == 1);
        product.BasePrice = 10;
        product.Name = "Renamed";
        product.IsAvailable = false;
        await fixture.Context.SaveChangesAsync();
        var replay = await fixture.CreateAsync(request, key);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.Order.Id, replay.Order.Id);
        Assert.Equal(3.50m, replay.Order.Subtotal);
        Assert.Equal("Tea", Assert.Single(replay.Order.Items).Name);
        Assert.Equal("No sugar", replay.Order.Note);
        Assert.Equal(1, await fixture.Context.Orders.CountAsync());
        fixture.Audit.Verify(audit => audit.RecordRestaurantActionAsync(It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ReorderedAndEquivalentMergedLinesHaveTheSameIntentHash()
    {
        await using var fixture = await Fixture.CreateAsync();
        var key = Guid.NewGuid();
        var first = await fixture.CreateAsync(new CreateWaiterOrderRequest { Items =
            [new() { ItemId = 1, Quantity = 1, Note = " hot " }, new() { ItemId = 2, Quantity = 1 },
             new() { ItemId = 1, Quantity = 1, Note = "hot" }] }, key);
        var replay = await fixture.CreateAsync(new CreateWaiterOrderRequest { Items =
            [new() { ItemId = 2, Quantity = 1 }, new() { ItemId = 1, Quantity = 2, Note = "hot" }] }, key);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.Order.Id, replay.Order.Id);
        Assert.Equal(2, replay.Order.Items.Count);
    }

    [Fact]
    public async Task KeyCannotBeReusedWithDifferentContentOrAfterSoftDeletion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var key = Guid.NewGuid();
        await fixture.CreateAsync(Request(), key);
        var conflict = await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.CreateAsync(Request(1, 2), key));
        Assert.Equal(ErrorCode.OrderRequestKeyConflict, conflict.Code);
        (await fixture.Context.Orders.SingleAsync()).IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.CreateAsync(Request(), key));
        Assert.Equal(1, await fixture.Context.Orders.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task NewKeyCreatesAnotherOrderForTheSameSession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreateAsync(Request());
        var next = await fixture.CreateAsync(Request());
        Assert.NotEqual(first.Order.Id, next.Order.Id);
        Assert.Equal(2, await fixture.Context.Orders.CountAsync());
    }

    [Fact]
    public async Task EqualDecimalPricesWithDifferentScaleReplayTheSameIntent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var key = Guid.NewGuid();
        var first = await fixture.CreateAsync(new CreateWaiterOrderRequest
            { Items = [new() { ItemId = 1, Quantity = 1, ExpectedUnitPrice = 3.50m }] }, key);
        var replay = await fixture.CreateAsync(new CreateWaiterOrderRequest
            { Items = [new() { ItemId = 1, Quantity = 1, ExpectedUnitPrice = 3.5000m }] }, key);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.Order.Id, replay.Order.Id);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(999)]
    public async Task ForeignClosedOrMissingSessionCannotCreateOrders(int sessionId)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.CreateForWaiterAsync(
            1, sessionId, Guid.NewGuid(), Request()));
        Assert.Empty(await fixture.Context.Orders.ToListAsync());
    }

    [Theory]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task StaleWaiterTokenDoesNotOverrideLiveStaffRole(RoleCode role)
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Context.UserRestaurants.SingleAsync()).RoleId = (int)role;
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.CreateAsync(Request()));
    }

    [Theory]
    [InlineData(3)] // Other restaurant.
    [InlineData(4)] // Inactive item.
    [InlineData(5)] // Unavailable flag.
    [InlineData(6)] // Out of stock status.
    [InlineData(8)] // Soft deleted item.
    [InlineData(9)] // Inactive category.
    [InlineData(10)] // Category belongs to another restaurant.
    [InlineData(11)] // Invalid currency precision.
    [InlineData(12)] // Line total exceeds storage precision.
    [InlineData(999)]
    public async Task InvalidOrUnavailableProductsDoNotPartiallyCreateAnOrder(int itemId)
    {
        await using var fixture = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.CreateAsync(
            new CreateWaiterOrderRequest { Items = [new() { ItemId = 1, Quantity = 1 }, new() { ItemId = itemId, Quantity = 2 }] }));
        Assert.Equal(ErrorCode.OrderItemUnavailable, error.Code);
        Assert.Empty(await fixture.Context.Orders.ToListAsync());
        Assert.Empty(await fixture.Context.OrderItems.ToListAsync());
    }

    [Fact]
    public async Task OptionalExpectedPriceOnlyDetectsStalenessAndNeverSetsThePrice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new CreateWaiterOrderRequest { Items = [new() { ItemId = 1, Quantity = 1, ExpectedUnitPrice = 1 }] };
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.CreateAsync(request));
        Assert.Equal(ErrorCode.OrderItemPriceChanged, error.Code);
        Assert.Empty(await fixture.Context.Orders.ToListAsync());
        Assert.Equal(3.5m, (await fixture.CreateAsync(Request())).Order.Subtotal);
        Assert.Equal(2m, (await fixture.CreateAsync(Request(7))).Order.Subtotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task InvalidQuantitiesAreRejected(int quantity)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(Request(1, quantity)));
    }

    [Fact]
    public async Task CombinedProductQuantityAcrossDifferentNotesCannotBypassLimit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest
        {
            Items = [new() { ItemId = 1, Quantity = 100 }, new() { ItemId = 1, Quantity = 1, Note = "extra" }]
        }));
    }

    [Fact]
    public async Task EmptyNullOversizedAndMissingKeyRequestsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest()));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest { Items = null! }));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest { Items = [null!] }));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest
            { Items = [new() { ItemId = 1, Quantity = 1 }], Note = new string('x', 1001) }));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateAsync(new CreateWaiterOrderRequest
            { Items = Enumerable.Range(1, 101).Select(_ => new CreateOrderLineRequest { ItemId = 1, Quantity = 1 }).ToList() }));
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateAsync(null!));
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Manager.CreateForWaiterAsync(1, 1, Guid.Empty, Request()));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("roleChanged")]
    [InlineData("reassigned")]
    public async Task SessionAndRoleAreRecheckedAfterAcquiringTableLock(string change)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.BeforeLock = async () =>
        {
            if (change == "roleChanged")
                (await fixture.Context.UserRestaurants.SingleAsync()).RoleId = (int)RoleCode.Kitchen;
            else
            {
                var session = await fixture.Context.TableSessions.SingleAsync(session => session.Id == 1);
                if (change == "closed") session.ClosedAt = DateTime.UtcNow;
                else session.WaiterUserId = 2;
            }
            await fixture.Context.SaveChangesAsync();
        };
        await Assert.ThrowsAnyAsync<BaseException>(() => fixture.CreateAsync(Request()));
        Assert.Empty(await fixture.Context.Orders.ToListAsync());
        fixture.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParallelSameKeyRequestsUnderTableLockCreateOnlyOneOrder()
    {
        var store = Guid.NewGuid().ToString();
        using var gate = new SemaphoreSlim(1, 1);
        await using var first = await Fixture.CreateAsync(store, gate);
        await using var second = await Fixture.CreateAsync(store, gate);
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(first.CreateAsync(Request(), key), second.CreateAsync(Request(), key));
        Assert.Single(results, result => result.IsReplay);
        Assert.Equal(results[0].Order.Id, results[1].Order.Id);
        Assert.Equal(1, await first.Context.Orders.CountAsync());
    }

    [Fact]
    public async Task ReadsUseSnapshotsAndRejectOtherSessionsWithPagination()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreateAsync(Request());
        var second = await fixture.CreateAsync(Request(2));
        var product = await fixture.Context.Set<Item>().SingleAsync(item => item.Id == 1);
        product.Name = "Changed";
        product.BasePrice = 100;
        await fixture.Context.SaveChangesAsync();
        var page = await fixture.Manager.GetForSessionAsync(1, 1, new PaginationFilter(1, 1));
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(second.Order.Id, Assert.Single(page.Items).Id);
        var detail = await fixture.Manager.GetByIdAsync(1, 1, first.Order.Id);
        Assert.Equal("Tea", Assert.Single(detail.Items).Name);
        Assert.Equal(3.5m, detail.Subtotal);
        Assert.Empty((await fixture.Manager.GetForSessionAsync(1, 1, new PaginationFilter(int.MaxValue, 100))).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.GetByIdAsync(1, 2, first.Order.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.GetByIdAsync(1, 1, 999));
    }

    [Fact]
    public async Task FailedAuditDoesNotCommitAndCancelledRequestDoesNotCreate()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Audit.Setup(audit => audit.RecordRestaurantActionAsync(It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateAsync(Request()));
        fixture.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        await using var cancelled = await Fixture.CreateAsync();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.Manager.CreateForWaiterAsync(
            1, 1, Guid.NewGuid(), Request(), source.Token));
        Assert.Empty(await cancelled.Context.Orders.ToListAsync());
    }

    [Fact]
    public async Task RequestLookupTranslatesForPostgreSqlAndUniqueIndexCoversDeletedOrders()
    {
        await using var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").AddInterceptors(new TranslationProbe()).Options);
        await Assert.ThrowsAsync<TranslationVerifiedException>(() =>
            new OrderRepository(context).FindCreationRequestAsync(1, 1, Guid.NewGuid()));
        var index = context.Model.FindEntityType(typeof(Order))!.GetIndexes().Single(index =>
            index.GetDatabaseName() == "orders_session_creation_request_uidx");
        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter());
        Assert.Equal(new[] { nameof(Order.TableSessionId), nameof(Order.CreationRequestId) }, index.Properties.Select(property => property.Name));
    }

    [Fact]
    public void ControllerHasContractGuardAndExplicitIdempotencyHeaderAndNoActorInput()
    {
        var method = typeof(OrderController).GetMethod(nameof(OrderController.Create))!;
        Assert.NotNull(method.GetCustomAttribute<RequireActiveRestaurantContractAttribute>());
        var header = Assert.Single(method.GetParameters(), parameter => parameter.GetCustomAttribute<FromHeaderAttribute>() != null);
        Assert.Equal("Idempotency-Key", header.GetCustomAttribute<FromHeaderAttribute>()!.Name);
        Assert.Equal(typeof(Guid), header.ParameterType);
        Assert.Null(typeof(CreateWaiterOrderRequest).GetProperty("WaiterUserId"));
        Assert.Null(typeof(CreateOrderLineRequest).GetProperty("UnitPrice"));
    }

    /// <summary>Məhsul və saydan sadə test sifarişi yaradır.</summary>
    private static CreateWaiterOrderRequest Request(int itemId = 1, int quantity = 1)
        => new() { Items = [new() { ItemId = itemId, Quantity = quantity }] };

    private sealed class TranslationVerifiedException : Exception;
    private sealed class TranslationProbe : DbConnectionInterceptor
    {
        /// <summary>SQL tərcüməsindən sonra bağlantını açmadan testi dayandırır; real bazaya müraciət etmir.</summary>
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new TranslationVerifiedException();
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Context { get; }
        public OrderManager Manager { get; }
        public DefaultHttpContext Http { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public Func<Task>? BeforeLock { get; set; }

        /// <summary>Həqiqi EF repository-lərini və giriş xidmətini, yalnız transaction/kilid/audit sərhədləri üçün test əvəzedicilərini qurur.</summary>
        private Fixture(ECafeDbContext context, SemaphoreSlim? gate)
        {
            Context = context;
            Http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("userId", "1"), new Claim("restaurantId", "1"),
                new Claim(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())
            }, "test"));
            var accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == Http);
            var mapper = Mock.Of<IMapper>();
            var configuration = new ConfigurationBuilder().Build();
            var sessions = new TableSessionRepository(context);
            var access = new TableSessionManager(accessor, mapper, configuration, sessions, new UserRestaurantRepository(context));
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(factory => factory.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            var tables = new Mock<ITableRepository>();
            var locked = false;
            tables.Setup(table => table.AcquireReservationLockAsync(1, 1, It.IsAny<CancellationToken>()))
                .Returns(async (int restaurantId, int tableId, CancellationToken token) =>
                {
                    if (BeforeLock is not null) await BeforeLock();
                    if (gate is not null) { await gate.WaitAsync(token); locked = true; }
                });
            Transaction.Setup(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return Task.CompletedTask;
            });
            Transaction.Setup(transaction => transaction.DisposeAsync()).Returns(() =>
            {
                if (locked) { gate!.Release(); locked = false; }
                return ValueTask.CompletedTask;
            });
            var limits = Options.Create(new OrderCreationOptions());
            Manager = new OrderManager(accessor, mapper, configuration, new OrderRepository(context), new ItemRepository(context),
                sessions, access, tables.Object, transactions.Object, Audit.Object,
                new CreateWaiterOrderValidator(limits), limits, TimeProvider.System);
        }

        /// <summary>İki restoran, ofisiantlar və müxtəlif menyu/sessiya vəziyyətləri ilə ayrılmış yaddaş bazası yaradır.</summary>
        public static async Task<Fixture> CreateAsync(string? store = null, SemaphoreSlim? gate = null)
        {
            var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
                .UseInMemoryDatabase(store ?? Guid.NewGuid().ToString()).Options);
            if (!await context.Restaurants.AnyAsync())
            {
                context.Restaurants.AddRange(
                    new Restaurant { Id = 1, Name = "One", Phone = "1", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true },
                    new Restaurant { Id = 2, Name = "Two", Phone = "2", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true });
                context.Tables.AddRange(new Table { Id = 1, RestaurantId = 1, TableNo = 1, Capacity = 4, IsActive = true },
                    new Table { Id = 2, RestaurantId = 2, TableNo = 1, Capacity = 4, IsActive = true });
                for (var id = 1; id <= 3; id++)
                    context.Users.Add(new User { Id = id, Name = "User", Surname = id.ToString(), Email = $"user{id}@example.test", Phone = id.ToString(), Password = "test", IsActive = true });
                context.UserRestaurants.Add(new UserRestaurant { Id = 1, UserId = 1, RestaurantId = 1, RoleId = (int)RoleCode.Waiter, IsActive = true });
                context.Reservations.Add(new Reservation { Id = 10, RestaurantId = 1, TableId = 1, CustomerUserId = 3, PeopleCount = 2 });
                for (var id = 1; id <= 4; id++)
                    context.TableSessions.Add(new TableSession { Id = id, RestaurantId = id == 3 ? 2 : 1, TableId = id == 3 ? 2 : 1,
                        WaiterUserId = id == 2 ? 2 : 1, CustomerUserId = 3, ReservationId = id == 1 ? 10 : null,
                        StatusId = StatusIds.TableSession(id == 4 ? TableSessionStatus.Closed : TableSessionStatus.Open), OpenedAt = DateTime.UtcNow });
                context.Categories.AddRange(new Category { Id = 1, RestaurantId = 1, Name = "Drinks", Slug = "drinks", IsActive = true },
                    new Category { Id = 2, RestaurantId = 2, Name = "Other", Slug = "other", IsActive = true },
                    new Category { Id = 3, RestaurantId = 1, Name = "Inactive", Slug = "inactive", IsActive = false });
                for (var id = 1; id <= 12; id++)
                    context.Set<Item>().Add(new Item { Id = id, RestaurantId = id == 3 ? 2 : 1,
                        CategoryId = id == 3 || id == 10 ? 2 : id == 9 ? 3 : 1,
                        Name = id == 1 ? "Tea" : $"Product {id}", BasePrice = id == 1 ? 3.5m : id == 11 ? 1.111m : id == 12 ? 99999999.99m : id == 7 ? 2 : 4,
                        IsActive = id != 4, IsAvailable = id != 5, IsDeleted = id == 8,
                        StatusId = StatusIds.Item(id == 6 ? ItemStatus.OutOfStock : id == 7 ? ItemStatus.Limited : ItemStatus.Available) });
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }
            return new Fixture(context, gate);
        }

        /// <summary>Varsayılan restoran/sessiya ilə yeni sorğu açarı istifadə edərək xidmət çağırır.</summary>
        public Task<CreateOrderResult> CreateAsync(CreateWaiterOrderRequest request, Guid? key = null)
            => Manager.CreateForWaiterAsync(1, 1, key ?? Guid.NewGuid(), request);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
