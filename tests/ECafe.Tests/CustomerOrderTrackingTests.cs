using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Api.Swagger;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Services.Orders;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Order;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class CustomerOrderTrackingTests
{
    [Fact]
    public async Task OwnSessionsIncludeClosedHistoryAndOtherRestaurantsWithStablePagination()
    {
        await using var f = await Fixture.CreateAsync();
        var page = await f.Manager.GetMySessionsAsync(new PaginationFilter(1, 1));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, Assert.Single(page.Items).Id);
        Assert.Equal(1, Assert.Single((await f.Manager.GetMySessionsAsync(new PaginationFilter(2, 1))).Items).Id);
        Assert.Equal(4, Assert.Single((await f.Manager.GetMySessionsAsync(new PaginationFilter(3, 1))).Items).Id);
        var detail = await f.Manager.GetSessionAsync(1);
        Assert.Equal("Masa-1", detail.TableName);
        Assert.Equal("Asia/Baku", detail.TimeZone);
        Assert.Equal(TimeSpan.Zero, detail.OpenedAt.Offset);
        Assert.Empty(f.Context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(2)] // Same table, different customer/session.
    [InlineData(5)] // Unclaimed walk-in.
    [InlineData(6)] // Deleted session.
    [InlineData(7)] // Table belongs to a different restaurant.
    [InlineData(8)] // Reservation belongs to a different customer.
    [InlineData(999)]
    public async Task ForeignOrUnavailableSessionIsAlwaysNotFound(int sessionId)
    {
        await using var f = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetSessionAsync(sessionId));
        Assert.Equal(ErrorCode.TableSessionNotFound, error.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetOrdersAsync(sessionId, new PaginationFilter()));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetOrderAsync(sessionId, 100));
    }

    [Fact]
    public async Task WaiterCreatedOrdersUseSessionOwnershipNotCreatorAndRetainSnapshotPrices()
    {
        await using var f = await Fixture.CreateAsync();
        var page = await f.Manager.GetOrdersAsync(1, new PaginationFilter(1, 1));
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(101, Assert.Single(page.Items).Id);
        var detail = await f.Manager.GetOrderAsync(1, 100);
        Assert.Equal(2003, detail.StatusId);
        Assert.Equal(10m, detail.Subtotal);
        var line = Assert.Single(detail.Items);
        Assert.Equal("Tea snapshot", line.Name);
        Assert.Equal(5m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.NotNull(detail.PreparingAt);
        Assert.Null(detail.ReadyAt);
        Assert.Null(detail.ServedAt);
        Assert.Equal(TimeSpan.Zero, detail.AcceptedAt!.Value.Offset);
        Assert.Empty(f.Context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(102)] // Another session, same table.
    [InlineData(103)] // Different restaurant, own other session.
    [InlineData(105)] // Deleted order.
    [InlineData(106)] // Inconsistent restaurant.
    [InlineData(107)] // Inconsistent customer.
    [InlineData(108)] // Inconsistent reservation.
    [InlineData(109)] // Inconsistent table.
    [InlineData(999)]
    public async Task OrderIdentifierCannotBypassSessionBoundary(int orderId)
    {
        await using var f = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetOrderAsync(1, orderId));
        Assert.Equal(ErrorCode.OrderNotFound, error.Code);
    }

    [Fact]
    public async Task ClosedSessionDoesNotInventServedStatusOrHideItsExistingOrders()
    {
        await using var f = await Fixture.CreateAsync();
        var session = await f.Manager.GetSessionAsync(4);
        Assert.NotNull(session.ClosedAt);
        var order = Assert.Single((await f.Manager.GetOrdersAsync(4, new PaginationFilter())).Items);
        Assert.Equal(2004, order.StatusId);
        Assert.NotNull(order.ReadyAt);
        Assert.Null(order.ServedAt);
        Assert.Null(order.ClosedAt);
    }

    [Fact]
    public async Task ReadReflectsKitchenProgressAndWaiterHandoffWithoutChangingOwnership()
    {
        await using var f = await Fixture.CreateAsync();
        var order = await f.Context.Orders.SingleAsync(o => o.Id == 100);
        order.StatusId = 2004;
        order.ReadyAt = Fixture.Now.AddMinutes(8);
        (await f.Context.TableSessions.SingleAsync(s => s.Id == 1)).WaiterUserId = null;
        await f.Context.SaveChangesAsync();
        f.Context.ChangeTracker.Clear();
        var detail = await f.Manager.GetOrderAsync(1, 100);
        Assert.Equal(2004, detail.StatusId);
        Assert.NotNull(detail.ReadyAt);
        Assert.Empty(f.Context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ChangingSessionOwnerRevokesAccessEvenWhenOrderStillContainsOldCustomerId()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Context.TableSessions.SingleAsync(s => s.Id == 1)).CustomerUserId = 2;
        await f.Context.SaveChangesAsync();
        Assert.DoesNotContain((await f.Manager.GetMySessionsAsync(new PaginationFilter())).Items, s => s.Id == 1);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetOrderAsync(1, 100));
    }

    [Theory]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task StaffProfileCannotUseCustomerEndpoints(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        f.Login(1, role);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.GetMySessionsAsync(new PaginationFilter()));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.GetOrderAsync(1, 100));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveOrDeletedUserCannotReadWithOldToken(bool deleted)
    {
        await using var f = await Fixture.CreateAsync();
        var user = await f.Context.Users.SingleAsync(u => u.Id == 1);
        user.IsDeleted = deleted;
        user.IsActive = deleted;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.GetSessionAsync(1));
    }

    [Fact]
    public async Task MissingIdentityIsRejectedAndActiveRestaurantHeaderCannotGrantAccess()
    {
        await using var f = await Fixture.CreateAsync();
        f.Http.Request.Headers["X-Active-Restaurant-Id"] = "2";
        Assert.Equal(1, (await f.Manager.GetSessionAsync(1)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetSessionAsync(2));
        f.Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Manager.GetSessionAsync(1));
    }

    [Fact]
    public async Task InactiveRestaurantOrTableDoesNotEraseCustomerHistory()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Context.Restaurants.SingleAsync(r => r.Id == 1)).IsActive = false;
        (await f.Context.Tables.SingleAsync(t => t.Id == 1)).IsActive = false;
        await f.Context.SaveChangesAsync();
        Assert.Equal(1, (await f.Manager.GetSessionAsync(1)).Id);
        Assert.Equal(100, (await f.Manager.GetOrderAsync(1, 100)).Id);
    }

    [Theory]
    [InlineData("restaurant")]
    [InlineData("table")]
    [InlineData("reservation")]
    public async Task DeletedParentCannotLeakSessionOrOrders(string parent)
    {
        await using var f = await Fixture.CreateAsync();
        if (parent == "restaurant") (await f.Context.Restaurants.SingleAsync(r => r.Id == 1)).IsDeleted = true;
        if (parent == "table") (await f.Context.Tables.SingleAsync(t => t.Id == 1)).IsDeleted = true;
        if (parent == "reservation") (await f.Context.Reservations.SingleAsync(r => r.Id == 1)).IsDeleted = true;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Manager.GetOrdersAsync(1, new PaginationFilter()));
    }

    [Fact]
    public async Task ResponseDoesNotExposeInternalNotesActorsPaymentOrContactDetails()
    {
        await using var f = await Fixture.CreateAsync();
        var json = JsonSerializer.Serialize(await f.Manager.GetOrderAsync(1, 100)).ToLowerInvariant();
        foreach (var forbidden in new[] { "note", "user", "payment", "refund", "email", "phone", "hash", "requestid", "internal" })
            Assert.DoesNotContain(forbidden, json);
        var sessionJson = JsonSerializer.Serialize(await f.Manager.GetSessionAsync(1)).ToLowerInvariant();
        Assert.DoesNotContain("closereview", sessionJson);
        Assert.DoesNotContain("note", sessionJson);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidIdsReturnBadRequest(int id)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.GetSessionAsync(id));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.GetOrdersAsync(id, new PaginationFilter()));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Manager.GetOrderAsync(1, id));
    }

    [Fact]
    public async Task PaginationHandlesEmptyOrdersClampsLimitsAndDoesNotOverflow()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Empty((await f.Manager.GetOrdersAsync(3, new PaginationFilter(99, 20))).Items);
        var filter = new PaginationFilter(0, 0);
        var page = await f.Manager.GetMySessionsAsync(filter);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(20, filter.PageSize);
        filter = new PaginationFilter(int.MaxValue, int.MaxValue);
        Assert.Empty((await f.Manager.GetOrdersAsync(1, filter)).Items);
        Assert.Equal(100, filter.PageSize);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        await using var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Manager.GetMySessionsAsync(new PaginationFilter(), cancellation.Token));
    }

    [Fact]
    public async Task LinkedWalkInWithoutOrdersReturnsEmptyPageNotAnotherTablesOrders()
    {
        await using var f = await Fixture.CreateAsync();
        f.Context.TableSessions.Add(new TableSession { Id = 99, RestaurantId = 1, TableId = 1,
            CustomerUserId = 1, StatusId = 7001, OpenedAt = Fixture.Now.AddDays(1) });
        await f.Context.SaveChangesAsync();
        var session = await f.Manager.GetSessionAsync(99);
        Assert.Null(session.ReservationId);
        var orders = await f.Manager.GetOrdersAsync(99, new PaginationFilter());
        Assert.Equal(0, orders.TotalCount);
        Assert.Empty(orders.Items);
    }

    [Theory]
    [InlineData(2001)]
    [InlineData(2002)]
    [InlineData(2003)]
    [InlineData(2004)]
    [InlineData(2005)]
    [InlineData(2006)]
    [InlineData(2007)]
    [InlineData(2008)]
    public async Task PersistedStatusIsNeverInferredFromSessionClosureOrPayment(int statusId)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Context.Orders.SingleAsync(o => o.Id == 100)).StatusId = statusId;
        await f.Context.SaveChangesAsync();
        Assert.Equal(statusId, (await f.Manager.GetOrderAsync(1, 100)).StatusId);
    }

    [Fact]
    public async Task OwnershipIsRecheckedInOrderQueryAfterInitialSessionCheck()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Manager.GetSessionAsync(1);
        var query = (IQueryable<Order>)typeof(CustomerOrderTrackingManager)
            .GetMethod("QueryOwnedOrders", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(f.Manager, [1, 1])!;
        (await f.Context.TableSessions.SingleAsync(s => s.Id == 1)).CustomerUserId = 2;
        await f.Context.SaveChangesAsync();
        Assert.Empty(await query.ToListAsync());
    }

    [Fact]
    public void ApiIsCustomerOnlyReadOnlyNoStoreAndDocumented()
    {
        var type = typeof(CustomerOrderController);
        Assert.Equal("5", type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.Contains("CustomerOrder", EcafeSwaggerMetadata.Tags.Keys);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(4, methods.Length);
        Assert.All(methods, method => Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>()));
        Assert.All(methods, method => Assert.DoesNotContain(method.GetParameters(), p => p.Name is "userId" or "customerUserId" or "restaurantId"));
    }

    [Fact]
    public async Task CustomerProjectionsTranslateToPostgresWithoutConnectingToDatabase()
    {
        await using var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Port=1;Database=translation;Username=unused").Options);
        var manager = Fixture.BuildManager(context, new DefaultHttpContext());
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var sessions = (IQueryable<TableSession>)typeof(CustomerOrderTrackingManager)
            .GetMethod("QueryOwnedSessions", flags)!.Invoke(manager, [1])!;
        var orders = (IQueryable<Order>)typeof(CustomerOrderTrackingManager)
            .GetMethod("QueryOwnedOrders", flags)!.Invoke(manager, [1, 1])!;
        var sessionView = (IQueryable<CustomerTableSessionResponse>)typeof(CustomerOrderTrackingManager)
            .GetMethod("ProjectSessions", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [sessions.OrderByDescending(s => s.OpenedAt).ThenByDescending(s => s.Id)])!;
        var orderView = (IQueryable<CustomerSessionOrderResponse>)typeof(CustomerOrderTrackingManager)
            .GetMethod("ProjectOrders", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [orders.OrderByDescending(o => o.OpenedAt).ThenByDescending(o => o.Id)])!;
        Assert.Contains("customer_user_id", sessionView.Skip(0).Take(20).ToQueryString());
        Assert.Contains("EXISTS", orderView.Skip(0).Take(20).ToQueryString());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        internal ECafeDbContext Context { get; }
        internal DefaultHttpContext Http { get; } = new();
        internal CustomerOrderTrackingManager Manager { get; }

        /// <summary>Həqiqi repository-ləri müşahidə olunan müştəri kontekstinə bağlayır.</summary>
        private Fixture(ECafeDbContext context)
        {
            Context = context;
            Login(1, RoleCode.Customer);
            Manager = BuildManager(context, Http);
        }

        /// <summary>InMemory davranış və PostgreSQL tərcümə testləri üçün eyni xidmət asılılıqlarını qurur.</summary>
        internal static CustomerOrderTrackingManager BuildManager(ECafeDbContext context, DefaultHttpContext http)
            => new(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http), Mock.Of<IMapper>(),
                new ConfigurationBuilder().Build(), new TableSessionRepository(context),
                new OrderRepository(context), new UserRepository(context));

        /// <summary>Tokenin istifadəçi və aktiv rol kontekstini test ssenarisinə uyğun dəyişir.</summary>
        internal void Login(int userId, RoleCode role)
            => Http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("userId", userId.ToString()), new Claim(ClaimTypes.Role, ((int)role).ToString())
            }, "test"));

        /// <summary>Eyni masanın fərqli müştərilərini, köhnə sessiyaları və uyğunsuz aidiyyətləri olan təcrid edilmiş baza yaradır.</summary>
        internal static async Task<Fixture> CreateAsync()
        {
            var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            context.Restaurants.AddRange(
                new Restaurant { Id = 1, Name = "One", Phone = "1", Location = "Baku", IsActive = true },
                new Restaurant { Id = 2, Name = "Two", Phone = "2", Location = "Baku", IsActive = true });
            context.Tables.AddRange(new Table { Id = 1, RestaurantId = 1, TableNo = 1, IsActive = true },
                new Table { Id = 2, RestaurantId = 2, TableNo = 2, IsActive = true });
            for (var id = 1; id <= 3; id++) context.Users.Add(new User { Id = id, Name = "Test", Surname = "User",
                Email = $"user{id}@example.test", Phone = id.ToString(), Password = "unused", IsActive = true,
                RoleId = (int)(id == 3 ? RoleCode.Waiter : RoleCode.Customer) });
            context.Reservations.AddRange(
                new Reservation { Id = 1, CustomerUserId = 1, RestaurantId = 1, TableId = 1 },
                new Reservation { Id = 2, CustomerUserId = 2, RestaurantId = 1, TableId = 1 });
            for (var id = 1; id <= 8; id++) context.TableSessions.Add(new TableSession
            {
                Id = id, RestaurantId = id == 3 ? 2 : 1, TableId = id is 3 or 7 ? 2 : 1,
                CustomerUserId = id == 5 ? null : id == 2 ? 2 : 1, WaiterUserId = 3,
                ReservationId = id == 1 ? 1 : id is 2 or 8 ? 2 : null,
                StatusId = id == 4 ? 7002 : 7001, OpenedAt = id == 4 ? Now.AddDays(-1) : Now,
                ClosedAt = id == 4 ? Now.AddDays(-1).AddHours(2) : null,
                IsDeleted = id == 6, Note = "Internal session note"
            });
            for (var id = 100; id <= 109; id++) context.Orders.Add(new Order
            {
                Id = id, RestaurantId = id is 103 or 106 ? 2 : 1, TableId = id is 103 or 109 ? 2 : 1,
                TableSessionId = id == 102 ? 2 : id == 103 ? 3 : id == 104 ? 4 : 1,
                CustomerUserId = id is 102 or 107 ? 2 : id == 101 ? null : 1,
                ReservationId = id == 108 ? 2 : null, WaiterUserId = 3, SourceId = (int)OrderSource.Waiter,
                OpenedAt = Now, StatusId = id == 104 ? 2004 : id == 101 ? 2001 : 2003,
                SentToKitchenAt = id == 101 ? null : Now.AddMinutes(1),
                AcceptedAt = id == 101 ? null : Now.AddMinutes(2), PreparingAt = id == 101 ? null : Now.AddMinutes(3),
                ReadyAt = id == 104 ? Now.AddMinutes(8) : null, Note = "Internal order note", IsDeleted = id == 105,
                OrderItems = [new OrderItem { Id = id, ItemId = 1, NameSnapshot = "Tea snapshot", Quantity = 2,
                    UnitPrice = 99m, UnitPriceSnapshot = 5m, LineTotal = 10m, Note = "Internal line note" },
                    new OrderItem { Id = id + 1000, ItemId = 2, NameSnapshot = "Deleted", Quantity = 1,
                        UnitPriceSnapshot = 100m, LineTotal = 100m, IsDeleted = true }]
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(context);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
