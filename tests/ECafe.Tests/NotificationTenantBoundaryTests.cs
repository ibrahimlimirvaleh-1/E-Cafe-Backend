using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Features.MobileApp;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Notification.Concrete;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Notification;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ECafe.Shared.DTOs;

namespace ECafe.Tests;

public sealed class NotificationTenantBoundaryTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("page")]
    [InlineData("count")]
    [InlineData("read")]
    [InlineData("read-all")]
    public async Task RevokedProfileCannotReadOrChangeNotificationsWithAnOldToken(string operation)
    {
        await using var f = await Fixture.CreateAsync();
        await f.DisableFirstProfileAsync();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => f.ExecuteAsync(operation));

        Assert.Equal(ErrorCode.AccessDenied, error.Code);
        Assert.Equal(0, await f.Db.Notifications.CountAsync(value => value.IsRead));
        Assert.True(await f.Db.UserRestaurants.AnyAsync(value => value.RestaurantId == 2 && value.IsActive));
        f.Dispatcher.VerifyNoOtherCalls();
        f.Http.Request.Headers["X-Active-Restaurant-Id"] = "2";
        Assert.Equal(new[] { 2, 3 }, (await f.Service.GetMyNotificationsAsync()).Select(value => value.Id).Order().ToArray());
    }

    [Fact]
    public async Task RepositoryFailsClosedForRevokedMembershipAndMissingStaffScope()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Empty(await f.Repository.GetByUserAsync(5, null));
        await f.DisableFirstProfileAsync();
        Assert.Empty(await f.Repository.GetByUserAsync(5, 1));
        Assert.Equal(0, await f.Repository.GetUnreadCountAsync(5, 1));
        Assert.Empty(await f.Repository.GetUnreadByUserAsync(5, 1));
        Assert.Null(await f.Repository.GetByUserAndIdTrackedAsync(5, 1, 1));
        Assert.Null(await f.Repository.GetByUserAndIdTrackedAsync(5, 4, 2));
    }

    [Fact]
    public async Task RevocationBetweenServiceCheckAndQueryDoesNotExposeOldNotifications()
    {
        await using var f = await Fixture.CreateAsync();
        var assignments = new Mock<IUserRestaurantRepository>();
        assignments.Setup(value => value.GetActiveRoleIdAsync(5, 1)).Returns(async () =>
        {
            await f.DisableFirstProfileAsync();
            return (int?)RoleCode.Manager;
        });

        Assert.Empty(await f.CreateService(assignments.Object).GetMyNotificationsAsync());
        var page = await f.CreateService(assignments.Object).GetMyNotificationsPageAsync(new());
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.SuperAdmin)]
    public async Task PersonalNotificationsAcrossRestaurantsRemainAvailableOnlyToTheirOwner(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        var user = await f.Db.Users.SingleAsync(value => value.Id == 5);
        user.RoleId = (int)role;
        foreach (var assignment in await f.Db.UserRestaurants.ToListAsync()) assignment.IsActive = false;
        await f.Db.SaveChangesAsync();
        f.SetRole(role);

        Assert.Equal(new[] { 1, 2, 3 }, (await f.Service.GetMyNotificationsAsync()).Select(value => value.Id).Order().ToArray());
        Assert.Equal(3, (await f.Service.GetUnreadCountAsync()).Count);
        var page = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(2, 2));
        Assert.Single(page.Items);
        Assert.Equal(3, page.TotalCount);
        await f.Service.MarkAllAsReadAsync();
        Assert.Equal(3, await f.Db.Notifications.CountAsync(value => value.UserId == 5 && value.IsRead));
        Assert.False((await f.Db.Notifications.SingleAsync(value => value.Id == 4)).IsRead);
    }

    [Theory]
    [InlineData("account-inactive")]
    [InlineData("assignment-deleted")]
    [InlineData("restaurant-inactive")]
    [InlineData("restaurant-deleted")]
    public async Task RepositoryChecksCurrentAccountAssignmentAndRestaurantState(string change)
    {
        await using var f = await Fixture.CreateAsync();
        if (change == "account-inactive") (await f.Db.Users.SingleAsync(value => value.Id == 5)).IsActive = false;
        if (change == "assignment-deleted") (await f.Db.UserRestaurants.SingleAsync(value => value.RestaurantId == 1)).IsDeleted = true;
        if (change == "restaurant-inactive") (await f.Db.Restaurants.SingleAsync(value => value.Id == 1)).IsActive = false;
        if (change == "restaurant-deleted") (await f.Db.Restaurants.SingleAsync(value => value.Id == 1)).IsDeleted = true;
        await f.Db.SaveChangesAsync();
        Assert.Empty(await f.Repository.GetByUserAsync(5, 1));
        var page = await f.Repository.GetPageByUserAsync(5, 1, 1, 20, default);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ActiveProfileCanMarkOnlyItsOwnScopedAndGlobalNotifications()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.MarkAsReadAsync(1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.MarkAsReadAsync(2));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.MarkAsReadAsync(4));
        await f.Service.MarkAllAsReadAsync();
        Assert.Equal(new[] { 1, 3 }, await f.Db.Notifications.Where(value => value.IsRead).OrderBy(value => value.Id).Select(value => value.Id).ToArrayAsync());
    }

    [Fact]
    public async Task PaginationUsesDatabaseBoundsAndStableOrderWithoutExposingOtherOwnersOrRestaurants()
    {
        await using var f = await Fixture.CreateAsync();
        await f.AddNoticesAsync(45);
        var first = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(1, 20));
        var second = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(2, 20));
        var last = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(3, 20));

        Assert.Equal(Enumerable.Range(35, 20).Reverse(), first.Items.Select(value => value.Id));
        Assert.Equal(Enumerable.Range(15, 20).Reverse(), second.Items.Select(value => value.Id));
        Assert.Equal(new[] { 14, 13, 12, 11, 10, 3, 1 }, last.Items.Select(value => value.Id));
        Assert.Equal(47, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(2, second.PageIndex);
        Assert.False(first.HasPreviousPage);
        Assert.True(first.HasNextPage);
        Assert.True(last.HasPreviousPage);
        Assert.False(last.HasNextPage);
        Assert.Empty(first.Items.Select(value => value.Id).Intersect(second.Items.Select(value => value.Id)));
        Assert.Equal(47, (await f.Service.GetMyNotificationsAsync()).Count);
        f.Db.ChangeTracker.Clear();
        await f.Service.GetMyNotificationsPageAsync(new());
        Assert.Empty(f.Db.ChangeTracker.Entries<Notification>());
    }

    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-1, -1, 1, 20)]
    [InlineData(1, int.MaxValue, 1, 100)]
    [InlineData(2, 20, 2, 20)]
    [InlineData(3, 50, 3, 22)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, 0)]
    public async Task PaginationNormalizesInputCapsPageSizeAndAvoidsOffsetOverflow(int number, int size, int expectedPage, int expectedItems)
    {
        await using var f = await Fixture.CreateAsync();
        await f.AddNoticesAsync(120);
        var page = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(number, size));
        Assert.Equal(expectedPage, page.PageIndex);
        Assert.Equal(expectedItems, page.Items.Count);
        Assert.Equal(122, page.TotalCount);
    }

    [Fact]
    public async Task EmptyPageKeepsItsMetadataAndAnEmptyAccountHasZeroPages()
    {
        await using var f = await Fixture.CreateAsync();
        var beyond = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(99, 20));
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.TotalCount);
        Assert.Equal(1, beyond.TotalPages);
        foreach (var notification in await f.Db.Notifications.Where(value => value.UserId == 5).ToListAsync()) notification.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        var empty = await f.Service.GetMyNotificationsPageAsync(new());
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNextPage);
    }

    [Fact]
    public async Task ReadStateAndUnreadCountApplyAcrossEveryPageButStayWithinTheCurrentScope()
    {
        await using var f = await Fixture.CreateAsync();
        await f.AddNoticesAsync(45);
        await f.Service.MarkAsReadAsync(54);
        Assert.Equal(46, (await f.Service.GetUnreadCountAsync()).Count);
        var first = await f.Service.GetMyNotificationsPageAsync(new());
        Assert.True(first.Items[0].IsRead);
        Assert.Equal(47, first.TotalCount);
        await f.Service.MarkAllAsReadAsync();
        var second = await f.Service.GetMyNotificationsPageAsync(new PaginationFilter(2, 20));
        Assert.All(second.Items, value => Assert.True(value.IsRead));
        Assert.Equal(0, (await f.Service.GetUnreadCountAsync()).Count);
        Assert.False((await f.Db.Notifications.SingleAsync(value => value.Id == 2)).IsRead);
        Assert.False((await f.Db.Notifications.SingleAsync(value => value.Id == 4)).IsRead);
    }

    [Fact]
    public async Task OnlyTheSelectedPageIsLocalizedAndCancelledReadsStopImmediately()
    {
        await using var f = await Fixture.CreateAsync();
        await f.AddNoticesAsync(45);
        var texts = new Mock<INotificationTextService>();
        texts.Setup(value => value.LocalizeForReader(It.IsAny<Notification>(), It.IsAny<NotificationResponse>()))
            .Callback<Notification, NotificationResponse>((notification, response) => response.Title = "Localized " + notification.Id);
        var service = f.CreateService(new UserRestaurantRepository(f.Db), texts.Object);
        var page = await service.GetMyNotificationsPageAsync(new PaginationFilter(2, 10));
        Assert.Equal(10, page.Items.Count);
        Assert.All(page.Items, value => Assert.Equal("Localized " + value.Id, value.Title));
        texts.Verify(value => value.LocalizeForReader(It.IsAny<Notification>(), It.IsAny<NotificationResponse>()), Times.Exactly(10));
        Assert.All(await f.Db.Notifications.ToListAsync(), value => Assert.Equal("Synthetic", value.Title));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetMyNotificationsPageAsync(new(), cancellation.Token));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public DefaultHttpContext Http { get; } = new();
        public Mock<INotificationChangeDispatcher> Dispatcher { get; } = new();
        public NotificationRepository Repository => new(Db);
        public NotificationManager Service => CreateService(new UserRestaurantRepository(Db));

        /// <summary>Köhnə tokenin hər iki restoranı saxlamasını simulyasiya edir; canlı aidiyyət bazada ayrıca dəyişir.</summary>
        public void SetRole(RoleCode role) => Http.User = new(new ClaimsIdentity([
            new("userId", "5"), new(ClaimTypes.Role, ((int)role).ToString()), new("restaurantId", "1"),
            new("restaurantIds", "1,2"), new("restaurantRoles", "1:3,2:3")], "test"));

        /// <summary>Gerçək repository və mapper istifadə edir; push, e-poçt və real vaxt çatdırılması saxtadır.</summary>
        public NotificationManager CreateService(IUserRestaurantRepository assignments, INotificationTextService? texts = null)
            => new(new HttpContextAccessor { HttpContext = Http }, new MapperConfiguration(config =>
                    config.AddProfile<NotificationProfile>(), NullLoggerFactory.Instance).CreateMapper(),
                new ConfigurationBuilder().Build(), Repository, Mock.Of<IValidator<CreateNotificationRequest>>(),
                Dispatcher.Object, Mock.Of<IMobilePushOutboxWriter>(), Mock.Of<INotificationEmailOutboxWriter>(), assignments, texts);

        /// <summary>Köhnə siyahı, səhifəli oxuma və oxunma əməliyyatlarının eyni restoran giriş qaydasını tətbiq etdiyini yoxlayır.</summary>
        public async Task ExecuteAsync(string operation)
        {
            if (operation == "list") await Service.GetMyNotificationsAsync();
            else if (operation == "page") await Service.GetMyNotificationsPageAsync(new());
            else if (operation == "count") await Service.GetUnreadCountAsync();
            else if (operation == "read") await Service.MarkAsReadAsync(1);
            else await Service.MarkAllAsReadAsync();
        }

        /// <summary>Başqa restoranı və qlobal hesabı bağlamadan ilk profilin aidiyyətini ləğv edir.</summary>
        public async Task DisableFirstProfileAsync()
        {
            (await Db.UserRestaurants.SingleAsync(value => value.RestaurantId == 1)).IsActive = false;
            await Db.SaveChangesAsync();
        }

        /// <summary>İki restoran, iki istifadəçi və qlobal bildirişlərlə tenant sərhədləri üçün yaddaş bazası yaradır.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.SetRole(RoleCode.Manager);
            f.Http.Request.Headers["X-Active-Restaurant-Id"] = "1";
            f.Db.Users.AddRange(new User { Id = 5, Name = "Reader", Surname = "Test", Email = "reader@example.test", Phone = "5", Password = "synthetic", IsActive = true, RoleId = 3 },
                new User { Id = 9, Name = "Other", Surname = "Test", Email = "other@example.test", Phone = "9", Password = "synthetic", IsActive = true, RoleId = 5 });
            f.Db.Restaurants.AddRange(new Restaurant { Id = 1, Name = "A", Phone = "1", Location = "Test", IsActive = true },
                new Restaurant { Id = 2, Name = "B", Phone = "2", Location = "Test", IsActive = true });
            f.Db.UserRestaurants.AddRange(new UserRestaurant { UserId = 5, RestaurantId = 1, IsActive = true, RoleId = 3 },
                new UserRestaurant { UserId = 5, RestaurantId = 2, IsActive = true, RoleId = 3 });
            f.Db.Notifications.AddRange(Notice(1, 5, 1), Notice(2, 5, 2), Notice(3, 5, null), Notice(4, 9, 1));
            await f.Db.SaveChangesAsync();
            return f;
        }

        /// <summary>Şəxsi olmayan sintetik mətnlə müəyyən istifadəçi və restoran üçün oxunmamış bildiriş qurur.</summary>
        private static Notification Notice(int id, int userId, int? restaurantId) => new()
        { Id = id, UserId = userId, RestaurantId = restaurantId, StatusId = (int)NotificationStatus.Unread, Title = "Synthetic", Message = "Test" };

        /// <summary>Eyni tarixli əlavə qeydlərlə səhifələrarası ID sırasını, limitləri və başqa tenant qeydlərinin sayılmadığını yoxlamağa imkan verir.</summary>
        public async Task AddNoticesAsync(int count)
        {
            Db.Notifications.AddRange(Enumerable.Range(10, count).Select(id => Notice(id, 5, 1)));
            await Db.SaveChangesAsync();
            foreach (var notice in await Db.Notifications.ToListAsync()) notice.CreatedAt = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
            await Db.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
