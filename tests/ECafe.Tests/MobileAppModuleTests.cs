using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Restaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAppModuleTests
{
    [Fact]
    public async Task OwnerCannotEnableMobileModule()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context, RoleCode.Owner, ReadySettings());

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new UpdateMobileModuleCommand(1, true, true), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new UpdateMobilePublicationCommand(true), CancellationToken.None));
    }

    [Fact]
    public async Task PushCannotBeEnabledBeforeDeliveryIsReady()
    {
        await using var context = CreateContext();
        context.Restaurants.Add(new Restaurant { Id = 1, Name = "Test", Location = "Baku", Phone = "1", IsActive = true });
        await context.SaveChangesAsync();
        var settings = ReadySettings();
        settings["MobileApp:PushDeliveryReady"] = "false";
        var handler = CreateHandler(context, RoleCode.SuperAdmin, settings);

        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new UpdateMobileModuleCommand(1, true, true), CancellationToken.None));
        Assert.False(context.Restaurants.Single().MobilePushEnabled);
    }

    [Fact]
    public async Task PublicReleaseRequiresVerifiedReleasePublicationAndLinkPreference()
    {
        await using var context = CreateContext();
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", IsActive = true,
            MobilePushEnabled = false, ShowMobileDownloadLink = false
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 1, RestaurantId = 1, ContractNumber = "C1", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        await context.SaveChangesAsync();
        var handler = CreateHandler(context, RoleCode.SuperAdmin, ReadySettings());

        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);

        await handler.Handle(new UpdateMobilePublicationCommand(true), CancellationToken.None);
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);

        await handler.Handle(new UpdateMobileModuleCommand(1, false, true), CancellationToken.None);
        Assert.True((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);
        var restaurantRelease = await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None);
        Assert.True(restaurantRelease.IsVisible);
        Assert.True(restaurantRelease.Ready);
        Assert.Equal(123456L, restaurantRelease.SizeBytes);
        Assert.Equal("https://example.com/ecafe.apk", restaurantRelease.ApkUrl);

        await handler.Handle(new UpdateMobileModuleCommand(1, true, false), CancellationToken.None);
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);

        await handler.Handle(new UpdateMobileModuleCommand(1, true, true), CancellationToken.None);
        await handler.Handle(new UpdateMobilePublicationCommand(false), CancellationToken.None);
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);
        Assert.True((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);
    }

    [Fact]
    public async Task ReleaseReadinessDoesNotRequirePushDelivery()
    {
        await using var context = CreateContext();
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", IsActive = true,
            MobilePushEnabled = false, ShowMobileDownloadLink = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 1, RestaurantId = 1, ContractNumber = "C1", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        await context.SaveChangesAsync();

        var settings = ReadySettings();
        settings["MobileApp:PushDeliveryReady"] = "false";
        var handler = CreateHandler(context, RoleCode.SuperAdmin, settings);

        Assert.True((await handler.Handle(new GetMobilePublicationQuery(), CancellationToken.None)).ReleaseReady);
        Assert.True((await handler.Handle(new GetPublicMobileReleaseQuery(1), CancellationToken.None)).IsVisible);
        Assert.False(context.Restaurants.Single().MobilePushEnabled);
    }

    [Fact]
    public async Task InvalidReleaseMetadataCannotBePublished()
    {
        await using var context = CreateContext();
        var settings = ReadySettings();
        settings["MobileApp:Release:ApkUrl"] = "http://example.com/ecafe.apk";
        var handler = CreateHandler(context, RoleCode.SuperAdmin, settings);

        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new UpdateMobilePublicationCommand(true), CancellationToken.None));
        Assert.False((await handler.Handle(new GetPublicMobileReleaseQuery(null), CancellationToken.None)).IsVisible);
    }

    private static ECafeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ECafeDbContext(options);
    }

    private static MobileAppQueryHandler CreateHandler(
        ECafeDbContext context, RoleCode role, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, ((int)role).ToString())], "Test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new MobileAppQueryHandler(
            new RestaurantRepository(context),
            new MobileAppPublicationStore(context),
            configuration,
            accessor);
    }

    private static Dictionary<string, string?> ReadySettings() => new()
    {
        ["MobileApp:PushDeliveryReady"] = "true",
        ["MobileApp:ExpoProjectId"] = "95755058-a833-443c-92c6-1b5dedf866ab",
        ["MobileApp:Release:Ready"] = "true",
        ["MobileApp:Release:ApkUrl"] = "https://example.com/ecafe.apk",
        ["MobileApp:Release:Version"] = "1.0.0",
        ["MobileApp:Release:VersionCode"] = "1",
        ["MobileApp:Release:SizeBytes"] = "123456",
        ["MobileApp:Release:Sha256"] = new string('a', 64)
    };
}
