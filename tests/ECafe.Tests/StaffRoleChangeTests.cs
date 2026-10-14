using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Application.Services.User.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Role;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class StaffRoleChangeTests
{
    [Theory]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Kitchen)]
    public async Task RoleChangeUpdatesStaffListAndProfileAndInvalidatesSession(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        var response = await f.Service.UpdateRoleAsync(5, (int)role, 1);
        var staff = Assert.Single(await new UserRestaurantRepository(f.Db).GetRestaurantStaffAsync(1));
        var profile = await new UserRepository(f.Db).GetProfileByIdAsync(5);

        Assert.Equal((int)role, staff.RoleId);
        Assert.Equal((int)role, Assert.Single(profile!.UserRestaurants).RoleId);
        Assert.Equal((int)role, f.User.RoleId);
        Assert.Equal(2, f.User.SessionVersion);
        Assert.Equal(string.Empty, response.AccessToken);
        Assert.Equal(string.Empty, response.RefreshToken);
        f.Sessions.Verify(cache => cache.InvalidateAsync(5), Times.Once);
        f.Access.Verify(cache => cache.InvalidateAsync(5, 1), Times.Once);
        Assert.Single(f.Realtime.Invocations);
    }

    [Fact]
    public async Task LegacyBrokenAccountRoleDoesNotMakeAssignmentRepairANoop()
    {
        await using var f = await Fixture.CreateAsync();
        f.User.RoleId = (int)RoleCode.Manager;
        await f.Db.SaveChangesAsync();

        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Manager, 1);

        Assert.Equal((int)RoleCode.Manager, f.Assignment.RoleId);
        Assert.Equal(2, f.User.SessionVersion);
        f.Sessions.Verify(cache => cache.InvalidateAsync(5), Times.Once);
        f.Access.Verify(cache => cache.InvalidateAsync(5, 1), Times.Once);
    }

    [Fact]
    public async Task OnlySelectedRestaurantAssignmentChangesForMultiRestaurantStaff()
    {
        await using var f = await Fixture.CreateAsync(multiple: true);
        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Manager, 1);

        Assert.Equal((int)RoleCode.Manager, f.Assignment.RoleId);
        Assert.Equal((int)RoleCode.Kitchen, f.User.UserRestaurants.Single(item => item.RestaurantId == 2).RoleId);
        Assert.Equal((int)RoleCode.Waiter, f.User.RoleId);
        f.Access.Verify(cache => cache.InvalidateAsync(5, 1), Times.Once);
        f.Access.Verify(cache => cache.InvalidateAsync(5, 2), Times.Never);
    }

    [Fact]
    public async Task AmbiguousLegacyRequestRequiresRestaurantInsteadOfChangingFirstAssignment()
    {
        await using var f = await Fixture.CreateAsync(multiple: true);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateRoleAsync(5, (int)RoleCode.Manager));

        Assert.Equal(ErrorCode.RestaurantContextRequired, error.Code);
        Assert.Equal((int)RoleCode.Waiter, f.Assignment.RoleId);
        Assert.Equal(1, f.User.SessionVersion);
        Assert.Empty(f.Realtime.Invocations);
    }

    [Fact]
    public async Task LegacySingleAssignmentRequestRemainsSupported()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Manager);
        Assert.Equal((int)RoleCode.Manager, f.Assignment.RoleId);
    }

    [Fact]
    public async Task ManagerCannotChangeAnotherRestaurantsAssignment()
    {
        await using var f = await Fixture.CreateAsync(multiple: true);
        f.SetActor(RoleCode.Manager, 1);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateRoleAsync(5, (int)RoleCode.Manager, 2));
        Assert.Equal((int)RoleCode.Kitchen, f.User.UserRestaurants.Single(item => item.RestaurantId == 2).RoleId);
        Assert.Empty(f.Realtime.Invocations);
    }

    [Fact]
    public async Task ManagerCanChangeOwnRestaurantEvenWhenTargetsOtherAssignmentIsFirst()
    {
        await using var f = await Fixture.CreateAsync(multiple: true);
        f.SetActor(RoleCode.Manager, 2);
        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Waiter, 2);
        Assert.Equal((int)RoleCode.Waiter, f.User.UserRestaurants.Single(item => item.RestaurantId == 2).RoleId);
        Assert.Equal((int)RoleCode.Waiter, f.Assignment.RoleId);
    }

    [Fact]
    public async Task ManagerCannotPromoteStaffToPlatformAdmin()
    {
        await using var f = await Fixture.CreateAsync();
        f.SetActor(RoleCode.Manager, 1);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateRoleAsync(5, (int)RoleCode.SuperAdmin, 1));
        Assert.Equal((int)RoleCode.Waiter, f.User.RoleId);
        Assert.Equal(1, f.User.SessionVersion);
    }

    [Fact]
    public async Task ManagerCannotModifyPlatformAdminsStaffProfile()
    {
        await using var f = await Fixture.CreateAsync();
        f.User.RoleId = (int)RoleCode.SuperAdmin;
        await f.Db.SaveChangesAsync();
        f.SetActor(RoleCode.Manager, 1);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateRoleAsync(5, (int)RoleCode.Kitchen, 1));
        Assert.Equal((int)RoleCode.Waiter, f.Assignment.RoleId);
    }

    [Fact]
    public async Task AdminUpdatingStaffAssignmentPreservesGlobalPlatformAdminRole()
    {
        await using var f = await Fixture.CreateAsync();
        f.User.RoleId = (int)RoleCode.SuperAdmin;
        await f.Db.SaveChangesAsync();
        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Kitchen, 1);
        Assert.Equal((int)RoleCode.SuperAdmin, f.User.RoleId);
        Assert.Equal((int)RoleCode.Kitchen, f.Assignment.RoleId);
    }

    [Fact]
    public async Task ExistingOwnerRoleCannotBeDowngraded()
    {
        await using var f = await Fixture.CreateAsync();
        f.Assignment.RoleId = (int)RoleCode.Owner;
        f.User.RoleId = (int)RoleCode.Owner;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateRoleAsync(5, (int)RoleCode.Kitchen, 1));
        Assert.Equal(ErrorCode.OwnerRoleCannotBeChanged, error.Code);
        Assert.Equal((int)RoleCode.Owner, f.Assignment.RoleId);
    }

    [Fact]
    public async Task RepeatingSameRoleDoesNotInvalidateOrNotifyAgain()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.UpdateRoleAsync(5, (int)RoleCode.Waiter, 1);
        Assert.Equal(1, f.User.SessionVersion);
        Assert.Empty(f.Sessions.Invocations);
        Assert.Empty(f.Access.Invocations);
        Assert.Empty(f.Realtime.Invocations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(999)]
    public async Task UnsupportedStaffRoleIsRejected(int roleId)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.UpdateRoleAsync(5, roleId, 1));
        Assert.Equal((int)RoleCode.Waiter, f.Assignment.RoleId);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; } = new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User User { get; } = new()
        {
            Id = 5, Name = "Test", Surname = "Staff", Email = "staff@example.com", Phone = "1",
            Password = "test", IsActive = true, RoleId = (int)RoleCode.Waiter, SessionVersion = 1
        };
        public UserRestaurant Assignment { get; private set; } = null!;
        public Mock<IUserSessionStateCache> Sessions { get; } = new();
        public Mock<IUserRestaurantAccessCache> Access { get; } = new();
        public Mock<IUserRealtimeNotifier> Realtime { get; } = new();
        public DefaultHttpContext Http { get; } = new();
        public UserManager Service { get; }

        public Fixture()
        {
            SetActor(RoleCode.SuperAdmin);
            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<AuthResponseDto>(It.IsAny<object>()))
                .Returns(() => new AuthResponseDto { AccessToken = string.Empty, RefreshToken = string.Empty });
            var accessor = Mock.Of<IHttpContextAccessor>(value => value.HttpContext == Http);
            Service = new(accessor, mapper.Object, Mock.Of<IConfiguration>(),
                Mock.Of<IRestaurantRepository>(), new RoleRepository(Db), Mock.Of<IMinioService>(),
                Mock.Of<IFileRepository>(), new UserRepository(Db), Mock.Of<IEmailOutboxService>(),
                Mock.Of<IJwtService>(), Mock.Of<IUserRefreshTokenRepository>(), new UserRestaurantRepository(Db),
                Mock.Of<IPasswordSetupService>(), Mock.Of<IAuditLogService>(), Realtime.Object,
                Mock.Of<IApplicationDbTransactionFactory>(), Sessions.Object, Access.Object);
        }

        /// <summary>Platforma və ya konkret restoran meneceri kontekstini sınaq sorğusuna tətbiq edir.</summary>
        public void SetActor(RoleCode role, int? restaurantId = null)
            => Http.User = new(new ClaimsIdentity([
                new Claim("userId", "99"), new Claim(ClaimTypes.Role, ((int)role).ToString()),
                new Claim("restaurantId", restaurantId?.ToString() ?? "")
            ], "test"));

        /// <summary>Real repository-lərlə yoxlanılan hesab və bir və ya iki restoran aidiyyətini bazada yaradır.</summary>
        public static async Task<Fixture> CreateAsync(bool multiple = false)
        {
            var fixture = new Fixture();
            fixture.Db.Roles.AddRange(Enum.GetValues<RoleCode>().Select(role => new Role { Id = (int)role, Name = role.ToString() }));
            fixture.Assignment = new()
            {
                Id = 1, User = fixture.User, RoleId = (int)RoleCode.Waiter, IsActive = true,
                Restaurant = new Restaurant { Id = 1, Name = "A", Location = "Baku", Phone = "1", IsActive = true }
            };
            fixture.Db.UserRestaurants.Add(fixture.Assignment);
            if (multiple)
                fixture.Db.UserRestaurants.Add(new UserRestaurant
                {
                    Id = 2, User = fixture.User, RoleId = (int)RoleCode.Kitchen, IsActive = true,
                    Restaurant = new Restaurant { Id = 2, Name = "B", Location = "Baku", Phone = "2", IsActive = true }
                });
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
