using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Features.MobileApp;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Notification.Concrete;
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

namespace ECafe.Tests;

public sealed class NotificationTenantBoundaryTests
{
    [Theory]
    [InlineData("list")]
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
        public NotificationManager CreateService(IUserRestaurantRepository assignments)
            => new(new HttpContextAccessor { HttpContext = Http }, new MapperConfiguration(config =>
                    config.AddProfile<NotificationProfile>(), NullLoggerFactory.Instance).CreateMapper(),
                new ConfigurationBuilder().Build(), Repository, Mock.Of<IValidator<CreateNotificationRequest>>(),
                Dispatcher.Object, Mock.Of<IMobilePushOutboxWriter>(), Mock.Of<INotificationEmailOutboxWriter>(), assignments);

        /// <summary>Dörd ictimai oxuma/yazma əməliyyatının eyni restoran giriş qaydasını tətbiq etdiyini yoxlayır.</summary>
        public async Task ExecuteAsync(string operation)
        {
            if (operation == "list") await Service.GetMyNotificationsAsync();
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

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
