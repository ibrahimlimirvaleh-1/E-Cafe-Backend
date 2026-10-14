using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Api.Swagger;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Services.TableSessions.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class WaiterTableSessionTests
{
    [Fact]
    public async Task ListUsesCurrentWaiterAndRestaurantAndStableOldestOpenSessionOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var sessions = await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter(1, 1));
        Assert.Equal(3, sessions.TotalCount);
        Assert.Equal(3, sessions.TotalPages);
        Assert.Equal(10, Assert.Single(sessions.Items).Id);
        var second = await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter(2, 1));
        Assert.Equal(1, Assert.Single(second.Items).Id);
        var third = await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter(3, 1));
        Assert.Equal(11, Assert.Single(third.Items).Id);
        Assert.Empty(fixture.Context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(2)] // Other waiter.
    [InlineData(3)] // Other restaurant.
    [InlineData(4)] // Closed.
    [InlineData(5)] // Cancelled.
    [InlineData(6)] // Soft deleted.
    [InlineData(7)] // Open status with a closing timestamp.
    [InlineData(8)] // No assigned waiter.
    [InlineData(999)]
    public async Task DetailDoesNotRevealUnavailableOrForeignSessions(int sessionId)
    {
        await using var fixture = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Manager.GetMyOpenSessionAsync(1, sessionId));
        Assert.Equal(ErrorCode.TableSessionNotFound, error.Code);
    }

    [Fact]
    public async Task DetailReturnsReservationServiceFieldsButNoPaymentOrContactInformation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var detail = await fixture.Manager.GetMyOpenSessionAsync(1, 1);
        Assert.Equal(1, detail.TableId);
        Assert.Equal(10, detail.ReservationId);
        Assert.Equal(2, detail.PeopleCount);
        Assert.Equal("Guest Test", detail.CustomerName);
        Assert.Equal("Asia/Baku", detail.TimeZone);
        Assert.Equal(TimeSpan.Zero, detail.OpenedAt.Offset);
        Assert.Equal(TimeSpan.Zero, detail.MustVacateAt!.Value.Offset);
        var json = JsonSerializer.Serialize(detail).ToLowerInvariant();
        Assert.DoesNotContain("deposit", json);
        Assert.DoesNotContain("payment", json);
        Assert.DoesNotContain("refund", json);
        Assert.DoesNotContain("email", json);
        Assert.DoesNotContain("phone", json);
    }

    [Fact]
    public async Task OpenSessionWithoutReservationOrCustomerStillAppears()
    {
        await using var fixture = await Fixture.CreateAsync();
        var session = await fixture.Manager.GetMyOpenSessionAsync(1, 10);
        Assert.Null(session.ReservationId);
        Assert.Null(session.CustomerName);
        Assert.Null(session.PeopleCount);
        Assert.Null(session.MustVacateAt);
    }

    [Theory]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task LiveAssignmentRoleMustBeWaiterEvenWhenTokenStillSaysWaiter(RoleCode role)
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = await fixture.Context.UserRestaurants.SingleAsync();
        assignment.RoleId = (int)role;
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter()));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Manager.GetMyOpenSessionAsync(1, 1));
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("deletedAssignment")]
    [InlineData("user")]
    [InlineData("restaurant")]
    public async Task InactiveOrDeletedAccessCannotReadSessions(string target)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (target is "assignment" or "deletedAssignment")
        {
            var assignment = await fixture.Context.UserRestaurants.SingleAsync();
            assignment.IsActive = target != "assignment";
            assignment.IsDeleted = target == "deletedAssignment";
        }
        if (target == "user")
            (await fixture.Context.Users.SingleAsync(user => user.Id == 1)).IsActive = false;
        if (target == "restaurant")
            (await fixture.Context.Restaurants.SingleAsync(restaurant => restaurant.Id == 1)).IsActive = false;
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter()));
    }

    [Fact]
    public async Task RestaurantRouteAndMissingUserContextCannotBypassAuthorization()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Manager.GetMyOpenSessionsAsync(2, new PaginationFilter()));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Manager.GetMyOpenSessionAsync(2, 3));
        fixture.Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Manager.GetMyOpenSessionAsync(1, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidIdentifiersAreRejected(int value)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Manager.GetMyOpenSessionsAsync(value, new PaginationFilter()));
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Manager.GetMyOpenSessionAsync(1, value));
    }

    [Fact]
    public async Task PaginationNormalizesLimitsAndHugePagesDoNotOverflow()
    {
        await using var fixture = await Fixture.CreateAsync();
        var filter = new PaginationFilter(0, 0);
        var first = await fixture.Manager.GetMyOpenSessionsAsync(1, filter);
        Assert.Equal(1, first.PageIndex);
        Assert.Equal(20, filter.PageSize);
        Assert.Equal(3, first.Items.Count);
        filter = new PaginationFilter(int.MaxValue, int.MaxValue);
        var last = await fixture.Manager.GetMyOpenSessionsAsync(1, filter);
        Assert.Equal(100, filter.PageSize);
        Assert.Equal(3, last.TotalCount);
        Assert.Empty(last.Items);
    }

    [Fact]
    public async Task ClosingSessionImmediatelyRemovesItFromListAndDetail()
    {
        await using var fixture = await Fixture.CreateAsync();
        var session = await fixture.Context.TableSessions.SingleAsync(item => item.Id == 1);
        session.StatusId = StatusIds.TableSession(TableSessionStatus.Closed);
        session.ClosedAt = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();
        var page = await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter());
        Assert.DoesNotContain(page.Items, item => item.Id == 1);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.GetMyOpenSessionAsync(1, 1));
    }

    [Fact]
    public async Task DeactivatingTableDoesNotHideAnOngoingSession()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Context.Tables.SingleAsync(table => table.Id == 1)).IsActive = false;
        await fixture.Context.SaveChangesAsync();
        Assert.Equal(1, (await fixture.Manager.GetMyOpenSessionAsync(1, 1)).Id);
    }

    [Fact]
    public async Task ReassignedSessionImmediatelyStopsBeingVisibleToOriginalWaiter()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Context.TableSessions.SingleAsync(session => session.Id == 1)).WaiterUserId = 2;
        await fixture.Context.SaveChangesAsync();
        Assert.DoesNotContain((await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter())).Items,
            session => session.Id == 1);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.GetMyOpenSessionAsync(1, 1));
    }

    [Fact]
    public async Task CurrentActiveRestaurantSelectsOnlyItsWaiterAssignment()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.UserRestaurants.Add(new UserRestaurant
        {
            Id = 2, UserId = 1, RestaurantId = 2, RoleId = (int)RoleCode.Waiter, IsActive = true
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "1"), new Claim("restaurantIds", "1,2"),
            new Claim(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())
        }, "test"));
        fixture.Http.Request.Headers["X-Active-Restaurant-Id"] = "2";
        var page = await fixture.Manager.GetMyOpenSessionsAsync(2, new PaginationFilter());
        Assert.Equal(3, Assert.Single(page.Items).Id);
        Assert.Equal(2, (await fixture.Manager.GetMyOpenSessionAsync(2, 3)).RestaurantId);
    }

    [Fact]
    public async Task DeletedTableDoesNotExposeSessionDetails()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Context.Tables.SingleAsync(table => table.Id == 1)).IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        Assert.Empty((await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter())).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Manager.GetMyOpenSessionAsync(1, 1));
    }

    [Fact]
    public async Task CancelledReadPropagatesCancellation()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Manager.GetMyOpenSessionAsync(1, 1, cancellation.Token));
    }

    [Fact]
    public async Task EmptyListIsReadOnlyAndDoesNotOpenAnySession()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.TableSessions.RemoveRange(await fixture.Context.TableSessions.ToListAsync());
        await fixture.Context.SaveChangesAsync();
        var page = await fixture.Manager.GetMyOpenSessionsAsync(1, new PaginationFilter());
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task DetailProjectionTranslatesForPostgreSqlBeforeOpeningConnection()
    {
        await using var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only")
            .AddInterceptors(new TranslationProbe()).Options);
        await Assert.ThrowsAsync<TranslationVerifiedException>(() =>
            new TableSessionRepository(context).GetOpenByIdForWaiterAsync(1, 1, 1));
    }

    [Fact]
    public void ControllerHasPermissionGuardRouteOnlyIdsAndDocumentedReadEndpoints()
    {
        var controller = typeof(TableSessionController);
        Assert.Contains(controller.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.GetType() == typeof(AuthorizeAttribute));
        Assert.Equal($"Permission:{(int)PermissionCode.ManageOrders}",
            controller.GetCustomAttribute<HasPermissionAttribute>()!.Policy);
        var list = controller.GetMethod(nameof(TableSessionController.GetMyOpenSessions))!;
        var query = Assert.Single(list.GetParameters(), p => p.GetCustomAttribute<FromQueryAttribute>() != null);
        Assert.Equal(typeof(PaginationFilter), query.ParameterType);
        Assert.Null(query.ParameterType.GetProperty("WaiterUserId"));
        Assert.Null(query.ParameterType.GetProperty("RestaurantId"));
        Assert.Contains("TableSession.GetMyOpenSessions", EcafeSwaggerMetadata.Endpoints.Keys);
        Assert.Contains("TableSession.GetMyOpenSession", EcafeSwaggerMetadata.Endpoints.Keys);
        Assert.Equal(2, controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
    }

    private sealed class TranslationVerifiedException : Exception;

    private sealed class TranslationProbe : DbConnectionInterceptor
    {
        /// <summary>SQL tərcüməsi uğurlu olduqda real PostgreSQL bağlantısını açmadan testi dayandırır.</summary>
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new TranslationVerifiedException();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Context { get; }
        public DefaultHttpContext Http { get; } = new();
        public TableSessionManager Manager { get; }

        /// <summary>Həqiqi repository-lər və dəyişdirilə bilən ofisiant konteksti ilə təcrid edilmiş xidmət qurur.</summary>
        private Fixture(ECafeDbContext context)
        {
            Context = context;
            Http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("userId", "1"), new Claim("restaurantId", "1"),
                new Claim(ClaimTypes.Role, ((int)RoleCode.Waiter).ToString())
            }, "test"));
            Manager = new TableSessionManager(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == Http),
                Mock.Of<IMapper>(), new ConfigurationBuilder().Build(),
                new TableSessionRepository(context), new UserRestaurantRepository(context));
        }

        /// <summary>Öz, başqa ofisiant, başqa restoran və bağlanmış sessiyaları olan test bazasını hazırlayır.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            context.Restaurants.AddRange(
                new Restaurant { Id = 1, Name = "One", Phone = "1", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true },
                new Restaurant { Id = 2, Name = "Two", Phone = "2", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true });
            context.Tables.AddRange(new Table { Id = 1, RestaurantId = 1, TableNo = 1, Capacity = 4, IsActive = true },
                new Table { Id = 2, RestaurantId = 2, TableNo = 1, Capacity = 4, IsActive = true });
            context.Users.AddRange(
                new User { Id = 1, Name = "Waiter", Surname = "One", Email = "one@example.test", Phone = "1", Password = "test", IsActive = true },
                new User { Id = 2, Name = "Waiter", Surname = "Two", Email = "two@example.test", Phone = "2", Password = "test", IsActive = true },
                new User { Id = 3, Name = "Guest", Surname = "Test", Email = "guest@example.test", Phone = "3", Password = "test", IsActive = true });
            context.UserRestaurants.Add(new UserRestaurant { Id = 1, UserId = 1, RestaurantId = 1, RoleId = (int)RoleCode.Waiter, IsActive = true });
            context.Reservations.Add(new Reservation { Id = 10, RestaurantId = 1, TableId = 1, CustomerUserId = 3,
                PeopleCount = 2, MustVacateAt = new DateTime(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc) });
            var openedAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            for (var id = 1; id <= 11; id++)
            {
                context.TableSessions.Add(new TableSession
                {
                    Id = id, RestaurantId = id == 3 ? 2 : 1, TableId = id == 3 ? 2 : 1,
                    WaiterUserId = id == 8 ? null : id == 2 ? 2 : 1,
                    StatusId = StatusIds.TableSession(id == 4 ? TableSessionStatus.Closed :
                        id == 5 ? TableSessionStatus.Cancelled : TableSessionStatus.Open),
                    ClosedAt = id is 4 or 7 ? openedAt.AddHours(1) : null,
                    IsDeleted = id is 6 or 9, OpenedAt = id == 10 ? openedAt.AddDays(-3) : openedAt,
                    ReservationId = id == 1 ? 10 : null, CustomerUserId = id == 1 ? 3 : null
                });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(context);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
