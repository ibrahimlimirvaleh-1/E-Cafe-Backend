using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class MobilePushDeliveryTests
{
    private static readonly Guid ProjectId = Guid.Parse("95755058-a833-443c-92c6-1b5dedf866ab");
    private const string SessionId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Token = "ExpoPushToken[abcdefghijklmnopqrstuv]";

    [Fact]
    public async Task OutboxExpandsOnceAndReceiptCompletesWithoutPrivatePayload()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        var writer = new MobilePushOutboxWriter(context, configuration);
        context.Notifications.Add(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp,
            Title = "Private title", Message = "Private message"
        });
        writer.Enqueue(context.Notifications.Local.Single());
        await context.SaveChangesAsync();

        Assert.DoesNotContain("Private", context.OutboxEvents.Single().Payload);
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        Assert.Equal(1, await processor.ExpandOutboxAsync(10, CancellationToken.None));
        Assert.Equal(0, await processor.ExpandOutboxAsync(10, CancellationToken.None));
        Assert.Single(context.MobilePushDeliveries);

        Assert.Equal(1, await processor.SendPendingAsync(10, CancellationToken.None));
        var delivery = context.MobilePushDeliveries.Single();
        Assert.Equal(MobilePushDeliveryStatus.TicketPending, delivery.Status);
        Assert.Equal(Token, Assert.Single(transport.SentTokens));
        Assert.Equal("Private title", context.Notifications.Single().Title);

        delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();
        Assert.Equal(1, await processor.CheckReceiptsAsync(10, CancellationToken.None));
        Assert.Equal(MobilePushDeliveryStatus.ProviderAccepted, delivery.Status);
        Assert.Equal(0, await processor.SendPendingAsync(10, CancellationToken.None));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("module-off")]
    [InlineData("inactive-user")]
    [InlineData("account-switch")]
    public async Task StaleRecipientIsSuppressedAtSendTime(string change)
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        var writer = new MobilePushOutboxWriter(context, configuration);
        writer.Enqueue(new Notification { UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);

        switch (change)
        {
            case "revoked":
                context.UserRefreshTokens.Single().RevokedAt = DateTime.UtcNow;
                break;
            case "module-off":
                context.Restaurants.Single().MobilePushEnabled = false;
                break;
            case "inactive-user":
                context.Users.Single().IsActive = false;
                break;
            case "account-switch":
                context.MobilePushInstallations.Single().UserId = 2;
                break;
        }

        await context.SaveChangesAsync();
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(transport.SentTokens);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task OldTokenReceiptCannotDeactivateRotatedToken()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport { ReceiptResult = new(false, "DeviceNotRegistered") };
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);

        var installation = context.MobilePushInstallations.Single();
        installation.TokenHash = new string('B', 64);
        installation.ProtectedToken = protector.CreateProtector("ECafe.MobilePush.InstallationToken.v1")
            .Protect("ExpoPushToken[rotatedabcdefghijklmnop]");
        context.MobilePushDeliveries.Single().NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();

        await processor.CheckReceiptsAsync(10, CancellationToken.None);
        Assert.True(installation.IsActive);
        Assert.Equal(MobilePushDeliveryStatus.InvalidToken, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task OneOutboxEventCreatesOneDeliveryPerActiveDevice()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.MobilePushInstallations.Add(new MobilePushInstallation
        {
            Id = Guid.NewGuid(), UserId = 1, SessionId = SessionId, ExpoProjectId = ProjectId,
            TokenHash = new string('C', 64),
            ProtectedToken = protector.CreateProtector("ECafe.MobilePush.InstallationToken.v1")
                .Protect("ExpoPushToken[secondabcdefghijklmnop]"),
            IsActive = true, RegisteredAt = DateTime.UtcNow
        });
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        Assert.Equal(2, context.MobilePushDeliveries.Count());

        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Equal(2, transport.SentTokens.Count);
        Assert.Equal(2, context.MobilePushDeliveries.Count(x => x.Status == MobilePushDeliveryStatus.TicketPending));
    }

    [Fact]
    public async Task InvalidExpoTicketDeactivatesOnlyTheSentToken()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport { SendResult = new(false, null, "DeviceNotRegistered") };
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);

        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.False(context.MobilePushInstallations.Single().IsActive);
        Assert.Equal(MobilePushDeliveryStatus.InvalidToken, context.MobilePushDeliveries.Single().Status);
        Assert.Equal(0, await processor.SendPendingAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredOutboxEventDoesNotSendAfterWorkerResumes()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        var outboxEvent = context.OutboxEvents.Local.Single();
        outboxEvent.OccurredAt = DateTime.UtcNow.AddDays(-2);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(context.MobilePushDeliveries);
        Assert.Empty(transport.SentTokens);
        Assert.Equal("ExpiredBeforeDelivery", outboxEvent.LastError);
    }

    [Fact]
    public async Task TransientProviderFailureIsRetriedWithoutNewDeliveryRow()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport { FailSendCount = 1 };
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);

        await processor.SendPendingAsync(10, CancellationToken.None);
        var delivery = context.MobilePushDeliveries.Single();
        Assert.Equal(MobilePushDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.True(delivery.NextAttemptAt > DateTime.UtcNow);

        delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Equal(MobilePushDeliveryStatus.TicketPending, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Single(context.MobilePushDeliveries);
    }

    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration ReadyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:PushDeliveryReady"] = "true",
            ["MobileApp:ExpoProjectId"] = ProjectId.ToString()
        }).Build();

    private static async Task SeedEligibleRecipientAsync(
        ECafeDbContext context, IDataProtectionProvider protector)
    {
        context.Users.Add(new User
        {
            Id = 1, Name = "Test", Surname = "User", Email = "test@example.com",
            Phone = "1", Password = "test", IsActive = true
        });
        context.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = 1, SessionId = SessionId, TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", IsActive = true,
            MobilePushEnabled = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 1, RestaurantId = 1, ContractNumber = "C1", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        context.MobilePushInstallations.Add(new MobilePushInstallation
        {
            Id = Guid.NewGuid(), UserId = 1, SessionId = SessionId, ExpoProjectId = ProjectId,
            TokenHash = new string('A', 64),
            ProtectedToken = protector.CreateProtector("ECafe.MobilePush.InstallationToken.v1")
                .Protect(Token),
            IsActive = true, RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private sealed class FakeTransport : IExpoPushTransport
    {
        public List<string> SentTokens { get; } = [];
        public ExpoPushResult? SendResult { get; set; }
        public int FailSendCount { get; set; }
        public ExpoReceiptResult ReceiptResult { get; set; } = new(true, null);

        public Task<ExpoPushResult> SendAsync(string expoToken, Guid deliveryId, CancellationToken cancellationToken)
        {
            if (FailSendCount-- > 0)
                throw new HttpRequestException("Temporary failure");
            SentTokens.Add(expoToken);
            return Task.FromResult(SendResult ?? new ExpoPushResult(true, deliveryId.ToString(), null));
        }

        public Task<ExpoReceiptResult?> GetReceiptAsync(string ticketId, CancellationToken cancellationToken)
            => Task.FromResult<ExpoReceiptResult?>(ReceiptResult);
    }
}
