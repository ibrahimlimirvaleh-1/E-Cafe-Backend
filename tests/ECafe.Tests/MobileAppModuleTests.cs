using System.Security.Claims;
using System.Security.Cryptography;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAppModuleTests
{
    [Fact]
    public async Task OnlyPlatformAdminCanChangeAccessAndPublicPublishingCannotBeEnabled()
    {
        await using var context = CreateContext();
        AddRestaurant(context, 1);
        await context.SaveChangesAsync();

        var owner = CreateHandler(context, RoleCode.Owner, 1, ReadySettings());
        await Assert.ThrowsAsync<ForbiddenException>(() => owner.Handle(
            new UpdateMobileModuleCommand(1, false, true), CancellationToken.None));

        var admin = CreateHandler(context, RoleCode.SuperAdmin, 1, ReadySettings());
        await Assert.ThrowsAsync<BusinessRuleException>(() => admin.Handle(
            new UpdateMobilePublicationCommand(true), CancellationToken.None));
        var disabledWithStalePush = await admin.Handle(
            new UpdateMobileModuleCommand(1, true, false), CancellationToken.None);
        Assert.False(disabledWithStalePush.MobilePushEnabled);

        context.Restaurants.Single().MobilePushEnabled = true;
        await context.SaveChangesAsync();
        var disabled = await admin.Handle(new UpdateMobileModuleCommand(1, false, false), CancellationToken.None);
        Assert.False(disabled.MobilePushEnabled);
        Assert.False(disabled.ShowDownloadLink);
    }

    [Fact]
    public async Task StaffAccessRequiresOwnActiveAssignmentContractAndRestaurantSwitch()
    {
        await using var context = CreateContext();
        AddRestaurant(context, 1);
        AddRestaurant(context, 2);
        AddStaff(context, 7, 1, RoleCode.Manager);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, RoleCode.Manager, 7, ReadySettings());

        Assert.True((await handler.Handle(new GetStaffMobileAccessQuery(1), CancellationToken.None)).Enabled);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetStaffMobileAccessQuery(2), CancellationToken.None));
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);

        context.UserRestaurants.Single().IsActive = false;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetStaffMobileAccessQuery(1), CancellationToken.None));
        context.UserRestaurants.Single().IsActive = true;
        context.Restaurants.Single(x => x.Id == 1).ShowMobileDownloadLink = false;
        await context.SaveChangesAsync();
        Assert.False((await handler.Handle(new GetStaffMobileAccessQuery(1), CancellationToken.None)).Enabled);

        context.Restaurants.Single(x => x.Id == 1).ShowMobileDownloadLink = true;
        context.RestaurantContracts.Single(x => x.RestaurantId == 1).StatusId =
            StatusIds.Contract(ContractStatus.Expired);
        await context.SaveChangesAsync();
        Assert.False((await handler.Handle(new GetStaffMobileAccessQuery(1), CancellationToken.None)).Enabled);

        context.UserRestaurants.Single().RoleId = (int)RoleCode.Customer;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetStaffMobileAccessQuery(1), CancellationToken.None));
    }

    [Fact]
    public async Task CustomerAccessUsesCurrentAccountNotStaleRoleClaim()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 8, Name = "Customer", Surname = "User", Email = "customer@example.com",
            Phone = "123", Password = "x", IsActive = true, RoleId = (int)RoleCode.Customer
        });
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, RoleCode.Manager, 8, ReadySettings());

        Assert.True((await handler.Handle(new GetCustomerMobileAccessQuery(), CancellationToken.None)).Enabled);
        context.Users.Single().IsActive = false;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetCustomerMobileAccessQuery(), CancellationToken.None));

        context.Users.Single().IsActive = true;
        context.Users.Single().RoleId = (int)RoleCode.Manager;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetCustomerMobileAccessQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task ReleaseNeedsVerifiedPrivateFileAndNeverExposesPublicUrl()
    {
        var path = Path.GetTempFileName();
        try
        {
            var content = new byte[] { 1, 2, 3, 4, 5 };
            await System.IO.File.WriteAllBytesAsync(path, content);
            var settings = ReadySettings();
            settings["MobileApp:Release:ApkPath"] = path;
            settings["MobileApp:Release:SizeBytes"] = content.Length.ToString();
            settings["MobileApp:Release:Sha256"] = Convert.ToHexString(SHA256.HashData(content));

            await using var context = CreateContext();
            AddRestaurant(context, 1);
            AddStaff(context, 7, 1, RoleCode.Waiter);
            await context.SaveChangesAsync();
            var handler = CreateHandler(context, RoleCode.Waiter, 7, settings);

            var release = await handler.Handle(new GetStaffMobileReleaseQuery(1), CancellationToken.None);
            Assert.True(release.Ready);
            Assert.Equal("/api/v1/restaurants/1/mobile-app/download", release.DownloadPath);
            Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);

            settings["MobileApp:Release:Sha256"] = new string('a', 64);
            handler = CreateHandler(context, RoleCode.Waiter, 7, settings);
            Assert.False((await handler.Handle(new GetStaffMobileReleaseQuery(1), CancellationToken.None)).Ready);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public async Task PushCannotBeEnabledBeforeDeliveryIsReady()
    {
        await using var context = CreateContext();
        AddRestaurant(context, 1);
        await context.SaveChangesAsync();
        var settings = ReadySettings();
        settings["MobileApp:PushDeliveryReady"] = "false";
        var handler = CreateHandler(context, RoleCode.SuperAdmin, 1, settings);

        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new UpdateMobileModuleCommand(1, true, true), CancellationToken.None));
        Assert.False(context.Restaurants.Single().MobilePushEnabled);
    }

    private static void AddRestaurant(ECafeDbContext context, int id)
    {
        context.Restaurants.Add(new Restaurant
        {
            Id = id, Name = $"Restaurant {id}", Location = "Baku", Phone = "1",
            IsActive = true, ShowMobileDownloadLink = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = id, RestaurantId = id, ContractNumber = $"C{id}", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
    }

    private static void AddStaff(ECafeDbContext context, int userId, int restaurantId, RoleCode role)
    {
        context.Users.Add(new User
        {
            Id = userId, Name = "Staff", Surname = "User", Email = "staff@example.com",
            Phone = "123", Password = "x", IsActive = true, RoleId = (int)role
        });
        context.UserRestaurants.Add(new UserRestaurant
        {
            Id = userId, UserId = userId, RestaurantId = restaurantId,
            RoleId = (int)role, IsActive = true
        });
    }

    private static ECafeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ECafeDbContext(options);
    }

    private static MobileAppQueryHandler CreateHandler(
        ECafeDbContext context, RoleCode role, int userId, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, ((int)role).ToString()),
            new Claim("userId", userId.ToString())
        ], "Test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new MobileAppQueryHandler(
            new RestaurantRepository(context),
            new UserRepository(context),
            new UserRestaurantRepository(context),
            new MobileAppPublicationStore(context),
            new LocalMobileReleaseArtifactService(configuration),
            configuration,
            accessor);
    }

    private static Dictionary<string, string?> ReadySettings() => new()
    {
        ["MobileApp:PushDeliveryReady"] = "true",
        ["MobileApp:ExpoProjectId"] = "95755058-a833-443c-92c6-1b5dedf866ab",
        ["MobileApp:Release:Ready"] = "true",
        ["MobileApp:Release:Version"] = "1.0.0",
        ["MobileApp:Release:VersionCode"] = "1"
    };
}
