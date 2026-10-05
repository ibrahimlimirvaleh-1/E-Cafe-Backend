using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class MobilePushInstallationTests
{
    private static readonly Guid ProjectId = Guid.Parse("95755058-a833-443c-92c6-1b5dedf866ab");
    private const string TokenA = "ExpoPushToken[abcdefghijklmnopqrstuv]";
    private const string TokenB = "ExpoPushToken[abcdefghijklmnopqrstwxyz]";

    [Fact]
    public async Task RegisterRotatesProtectedTokenAndDeactivatesOwnInstallation()
    {
        await using var context = CreateContext();
        await AddSessionAsync(context, 1, "a");
        var service = CreateService(context, 1, "a");
        var installationId = Guid.NewGuid();

        await service.RegisterAsync(installationId, new(TokenA, ProjectId), CancellationToken.None);
        var firstHash = context.MobilePushInstallations.Single().TokenHash;
        Assert.DoesNotContain(TokenA, context.MobilePushInstallations.Single().ProtectedToken);

        await service.RegisterAsync(installationId, new(TokenB, ProjectId), CancellationToken.None);
        Assert.NotEqual(firstHash, context.MobilePushInstallations.Single().TokenHash);
        Assert.True(context.MobilePushInstallations.Single().IsActive);

        await service.DeactivateAsync(installationId, CancellationToken.None);
        Assert.False(context.MobilePushInstallations.Single().IsActive);
    }

    [Fact]
    public async Task AccountSwitchRequiresOldSessionToBeRevoked()
    {
        await using var context = CreateContext();
        await AddSessionAsync(context, 1, "a");
        await AddSessionAsync(context, 2, "b");
        var oldAccount = CreateService(context, 1, "a");
        var installationId = Guid.NewGuid();

        await oldAccount.RegisterAsync(installationId, new(TokenA, ProjectId), CancellationToken.None);
        var newAccount = CreateService(context, 2, "b");
        Assert.True(context.MobilePushInstallations.Single().IsActive);
        Assert.True(await context.UserRefreshTokens.AnyAsync(x => x.UserId == 1 &&
            x.SessionId == new string('a', 32) && x.RevokedAt == null &&
            x.ExpiresAt > DateTime.UtcNow && x.User.IsActive));
        await Assert.ThrowsAsync<BusinessRuleException>(() => newAccount.RegisterAsync(
            installationId, new(TokenB, ProjectId), CancellationToken.None));

        context.UserRefreshTokens.Single(x => x.UserId == 1).RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await newAccount.RegisterAsync(installationId, new(TokenB, ProjectId), CancellationToken.None);

        Assert.Equal(2, context.MobilePushInstallations.Single().UserId);
        CreateService(context, 1, "a");
        await oldAccount.DeactivateAsync(installationId, CancellationToken.None);
        Assert.True(context.MobilePushInstallations.Single().IsActive);
    }

    [Fact]
    public async Task RegistrationRejectsUnassignedStaffButAcceptsActiveCustomer()
    {
        await using var context = CreateContext();
        await AddSessionAsync(context, 1, "a");
        var installationId = Guid.NewGuid();
        await CreateService(context, 1, "a").RegisterAsync(
            installationId, new(TokenA, ProjectId), CancellationToken.None);

        context.Users.Single().RoleId = (int)RoleCode.Manager;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(context, 1, "a")
            .RegisterAsync(Guid.NewGuid(), new(TokenB, ProjectId), CancellationToken.None));
    }

    [Fact]
    public async Task RegistrationRequiresConfiguredProjectAndActiveSession()
    {
        await using var context = CreateContext();
        await AddSessionAsync(context, 1, "a");
        var service = CreateService(context, 1, "a", ready: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegisterAsync(
            Guid.NewGuid(), new(TokenA, ProjectId), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(context, 1, "a")
            .RegisterAsync(Guid.NewGuid(), new(TokenA, Guid.NewGuid()), CancellationToken.None));

        context.UserRefreshTokens.Single().RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedException>(() => CreateService(context, 1, "a")
            .RegisterAsync(Guid.NewGuid(), new(TokenA, ProjectId), CancellationToken.None));
    }

    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task AddSessionAsync(ECafeDbContext context, int userId, string sessionCharacter)
    {
        context.Users.Add(new User
        {
            Id = userId, Name = "Test", Surname = "User", Email = $"user{userId}@example.com",
            Phone = userId.ToString(), Password = "test", IsActive = true,
            RoleId = (int)RoleCode.Customer
        });
        context.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = userId, SessionId = new string(sessionCharacter[0], 32),
            TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await context.SaveChangesAsync();
    }

    private static MobilePushInstallationService CreateService(
        ECafeDbContext context, int userId, string sessionCharacter, bool ready = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:PushDeliveryReady"] = ready.ToString(),
            ["MobileApp:ExpoProjectId"] = ProjectId.ToString()
        }).Build();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", userId.ToString()),
            new Claim("sessionId", new string(sessionCharacter[0], 32))
        ], "Test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };
        return new(context, accessor, configuration, new EphemeralDataProtectionProvider());
    }
}
