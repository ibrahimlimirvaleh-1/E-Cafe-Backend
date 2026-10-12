using ECafe.Domain.Entities;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
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
    [InlineData("assignment-off")]
    [InlineData("contract-expired")]
    [InlineData("mobile-access-off")]
    [InlineData("restaurant-off")]
    [InlineData("non-staff-role")]
    [InlineData("changed-account-role")]
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
            case "assignment-off":
                context.UserRestaurants.Single().IsActive = false;
                break;
            case "contract-expired":
                context.RestaurantContracts.Single().StatusId = StatusIds.Contract(ContractStatus.Expired);
                break;
            case "mobile-access-off":
                context.Restaurants.Single().ShowMobileDownloadLink = false;
                break;
            case "restaurant-off":
                context.Restaurants.Single().IsActive = false;
                break;
            case "non-staff-role":
                context.UserRestaurants.Single().RoleId = (int)RoleCode.Customer;
                break;
            case "changed-account-role":
                context.Users.Single().RoleId = (int)RoleCode.Customer;
                break;
        }

        await context.SaveChangesAsync();
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(transport.SentTokens);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task StaffPushRequiresMatchingActiveRestaurantAssignmentAtExpansionAndSend()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, new FakeTransport());

        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        Assert.Single(context.MobilePushDeliveries);

        context.UserRestaurants.Single().IsActive = false;
        await context.SaveChangesAsync();
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);

        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        Assert.Single(context.MobilePushDeliveries);
    }

    [Theory]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    public async Task OnlyStaffInAdminGrantedRestaurantReceivePush(RoleCode role)
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.UserRestaurants.Single().RoleId = (int)role;
        context.Restaurants.Add(new Restaurant
        {
            Id = 2, Name = "Not granted", Location = "Baku", Phone = "2", IsActive = true,
            MobilePushEnabled = false, ShowMobileDownloadLink = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 2, RestaurantId = 2, ContractNumber = "C2", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        context.UserRestaurants.Add(new UserRestaurant
        {
            Id = 2, UserId = 1, RestaurantId = 2, RoleId = (int)role, IsActive = true
        });
        var configuration = ReadyConfiguration();
        var writer = new MobilePushOutboxWriter(context, configuration);
        writer.Enqueue(new Notification { UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp });
        writer.Enqueue(new Notification { UserId = 1, RestaurantId = 2, ChannelId = (int)NotificationChannel.InApp });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        Assert.Equal(1, Assert.Single(context.MobilePushDeliveries).RestaurantId);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Equal(Token, Assert.Single(transport.SentTokens));
    }

    [Fact]
    public async Task AssignmentToOtherGrantedRestaurantCannotReceiveThisRestaurantsPush()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.Restaurants.Add(new Restaurant
        {
            Id = 2, Name = "Other granted restaurant", Location = "Baku", Phone = "2", IsActive = true,
            MobilePushEnabled = true, ShowMobileDownloadLink = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 2, RestaurantId = 2, ContractNumber = "C2", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        context.UserRestaurants.Single().IsActive = false;
        context.UserRestaurants.Add(new UserRestaurant
        {
            Id = 2, UserId = 1, RestaurantId = 2, RoleId = (int)RoleCode.Manager, IsActive = true
        });
        var configuration = ReadyConfiguration();
        var writer = new MobilePushOutboxWriter(context, configuration);
        writer.Enqueue(new Notification { UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp });
        writer.Enqueue(new Notification { UserId = 1, RestaurantId = 2, ChannelId = (int)NotificationChannel.InApp });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        Assert.Equal(2, Assert.Single(context.MobilePushDeliveries).RestaurantId);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Single(transport.SentTokens);
    }

    [Fact]
    public async Task ExistingManagerAssignmentRemainsAuthoritativeForNonCustomerAccount()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.Users.Single().RoleId = (int)RoleCode.SuperAdmin;
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);

        Assert.Single(transport.SentTokens);
    }

    [Theory]
    [InlineData(AuditEntityTypes.Order)]
    [InlineData(AuditEntityTypes.ReservationRefund)]
    public async Task CustomerCannotReceiveAnotherCustomersOrderOrRefund(string entityType)
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        var notification = await SeedCustomerNotificationAsync(context, protector, entityType);
        if (entityType == AuditEntityTypes.Order)
            context.Orders.Single().CustomerUserId = 2;
        else
            context.Reservations.Single().CustomerUserId = 2;
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);

        Assert.Empty(context.MobilePushDeliveries);
        Assert.Empty(transport.SentTokens);
    }

    [Fact]
    public async Task CustomerPhonePushWithoutResourceLinkIsSuppressedButInboxIsRetained()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.UserRestaurants.RemoveRange(context.UserRestaurants);
        context.Users.Single().RoleId = (int)RoleCode.Customer;
        var notification = new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp,
            Title = "Customer inbox", Message = "Still visible in the app"
        };
        context.Notifications.Add(notification);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(context.MobilePushDeliveries);
        Assert.Empty(transport.SentTokens);
        Assert.Equal("Customer inbox", Assert.Single(context.Notifications).Title);
    }

    [Theory]
    [InlineData(AuditEntityTypes.Reservation)]
    [InlineData(AuditEntityTypes.Order)]
    [InlineData(AuditEntityTypes.ReservationRefund)]
    public async Task CustomerReceivesOwnResourcePushWithoutRestaurantStaffGrant(string entityType)
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        var notification = await SeedCustomerNotificationAsync(context, protector, entityType);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        Assert.Equal(1, await processor.ExpandOutboxAsync(10, CancellationToken.None));
        Assert.Single(context.MobilePushDeliveries);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Equal(Token, Assert.Single(transport.SentTokens));
        Assert.Equal(MobilePushDeliveryStatus.TicketPending, context.MobilePushDeliveries.Single().Status);
        Assert.DoesNotContain(notification.Message, context.OutboxEvents.Single().Payload);
    }

    [Theory]
    [InlineData("inactive-user", false)]
    [InlineData("inactive-user", true)]
    [InlineData("expired-session", false)]
    [InlineData("expired-session", true)]
    [InlineData("revoked-session", false)]
    [InlineData("revoked-session", true)]
    [InlineData("changed-role", false)]
    [InlineData("changed-role", true)]
    [InlineData("wrong-customer", false)]
    [InlineData("wrong-customer", true)]
    [InlineData("wrong-restaurant", false)]
    [InlineData("wrong-restaurant", true)]
    [InlineData("deleted-resource", false)]
    [InlineData("deleted-resource", true)]
    public async Task CustomerEligibilityIsRecheckedAtExpansionAndSend(string change, bool afterExpansion)
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        var notification = await SeedCustomerNotificationAsync(context, protector, AuditEntityTypes.Reservation);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        if (afterExpansion)
            await processor.ExpandOutboxAsync(10, CancellationToken.None);

        switch (change)
        {
            case "inactive-user": context.Users.Single().IsActive = false; break;
            case "expired-session": context.UserRefreshTokens.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1); break;
            case "revoked-session": context.UserRefreshTokens.Single().RevokedAt = DateTime.UtcNow; break;
            case "changed-role": context.Users.Single().RoleId = (int)RoleCode.SuperAdmin; break;
            case "wrong-customer": context.Reservations.Single().CustomerUserId = 2; break;
            case "wrong-restaurant": context.Reservations.Single().RestaurantId = 2; break;
            case "deleted-resource": context.Reservations.Single().IsDeleted = true; break;
        }
        await context.SaveChangesAsync();
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);

        Assert.Empty(transport.SentTokens);
        if (afterExpansion)
            Assert.Equal(MobilePushDeliveryStatus.Suppressed, Assert.Single(context.MobilePushDeliveries).Status);
        else
            Assert.Empty(context.MobilePushDeliveries);
        Assert.Single(context.Notifications);
    }

    [Fact]
    public async Task ChangedDeliveryRecipientCannotRedirectOriginalEventToAnotherCustomer()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        var notification = await SeedCustomerNotificationAsync(context, protector, AuditEntityTypes.Reservation);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        context.MobilePushDeliveries.Single().UserId = 2;
        await context.SaveChangesAsync();

        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(transport.SentTokens);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task LegacyStaffPayloadRemainsSupportedOnlyForEligibleStaff()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.NewGuid(), EventType = OutboxEventTypes.MobilePushRequested,
            AggregateType = "Notification", Payload = "{\"UserId\":1,\"RestaurantId\":1}",
            OccurredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, ReadyConfiguration(), protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Single(transport.SentTokens);
    }

    [Fact]
    public async Task MalformedEventAfterExpansionIsSuppressed()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        var notification = await SeedCustomerNotificationAsync(context, protector, AuditEntityTypes.Reservation);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(notification);
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        context.OutboxEvents.Single().Payload = "invalid-json";
        await context.SaveChangesAsync();

        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(transport.SentTokens);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task StaffRoleChangedToCustomerCannotReceivePreviouslyExpandedStaffEvent()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp,
            RelatedEntityType = AuditEntityTypes.Reservation, RelatedEntityId = 10
        });
        context.Reservations.Add(new Reservation
        {
            Id = 10, RestaurantId = 1, TableId = 1, CustomerUserId = 2,
            PeopleCount = 2, ReservedAt = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);
        await processor.ExpandOutboxAsync(10, CancellationToken.None);
        context.Users.Single().RoleId = (int)RoleCode.Customer;
        await context.SaveChangesAsync();

        await processor.SendPendingAsync(10, CancellationToken.None);
        Assert.Empty(transport.SentTokens);
        Assert.Equal(MobilePushDeliveryStatus.Suppressed, context.MobilePushDeliveries.Single().Status);
    }

    [Fact]
    public async Task GlobalSwitchOffPreventsNewPhonePushEvents()
    {
        await using var context = CreateContext();
        var protector = new EphemeralDataProtectionProvider();
        await SeedEligibleRecipientAsync(context, protector);
        var configuration = ReadyConfiguration();
        configuration["MobileApp:PushDeliveryReady"] = "false";
        new MobilePushOutboxWriter(context, configuration).Enqueue(new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp
        });
        await context.SaveChangesAsync();
        var transport = new FakeTransport();
        var processor = new MobilePushDeliveryProcessor(context, configuration, protector, transport);

        Assert.Equal(0, await processor.ExpandOutboxAsync(10, CancellationToken.None));
        Assert.Equal(0, await processor.SendPendingAsync(10, CancellationToken.None));
        Assert.Empty(context.OutboxEvents);
        Assert.Empty(transport.SentTokens);
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

    /// <summary>Sınaqların hesab, sessiya və göndəriş vəziyyətini ayrıca yaddaş bazasında təcrid edir.</summary>
    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration ReadyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:PushDeliveryReady"] = "true",
            ["MobileApp:ExpoProjectId"] = ProjectId.ToString()
        }).Build();

    /// <summary>Müştərinin öz rezervasiya, sifariş və refund obyektlərini, sessiyasını və əlaqəli server bildirişini yaradır; işçi icazələri bağlıdır.</summary>
    private static async Task<Notification> SeedCustomerNotificationAsync(
        ECafeDbContext context, IDataProtectionProvider protector, string entityType)
    {
        await SeedEligibleRecipientAsync(context, protector);
        context.Users.Single().RoleId = (int)RoleCode.Customer;
        context.UserRestaurants.RemoveRange(context.UserRestaurants);
        context.Restaurants.Single().MobilePushEnabled = false;
        context.Restaurants.Single().ShowMobileDownloadLink = false;
        context.RestaurantContracts.Single().StatusId = StatusIds.Contract(ContractStatus.Expired);
        var reservation = new Reservation
        {
            Id = 10, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            PeopleCount = 2, ReservedAt = DateTime.UtcNow.AddHours(1)
        };
        context.Reservations.Add(reservation);
        context.Orders.Add(new Order { Id = 20, RestaurantId = 1, TableId = 1, CustomerUserId = 1 });
        context.ReservationRefunds.Add(new ReservationRefund
        {
            Id = 30, Reservation = reservation, Amount = 20, EligibilityReason = "Test refund"
        });
        var notification = new Notification
        {
            UserId = 1, RestaurantId = 1, ChannelId = (int)NotificationChannel.InApp,
            Title = "Customer inbox", Message = "Private customer details",
            RelatedEntityType = entityType,
            RelatedEntityId = entityType switch
            {
                AuditEntityTypes.Reservation => 10,
                AuditEntityTypes.Order => 20,
                AuditEntityTypes.ReservationRefund => 30,
                _ => throw new ArgumentOutOfRangeException(nameof(entityType))
            }
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    /// <summary>Test üçün icazəli restoran, aktiv işçi aidiyyəti və qeydiyyatlı telefon sessiyası yaradır.</summary>
    private static async Task SeedEligibleRecipientAsync(
        ECafeDbContext context, IDataProtectionProvider protector)
    {
        context.Users.Add(new User
        {
            Id = 1, Name = "Test", Surname = "User", Email = "test@example.com",
            Phone = "1", Password = "test", IsActive = true,
            RoleId = (int)RoleCode.Manager
        });
        context.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = 1, SessionId = SessionId, TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", IsActive = true,
            MobilePushEnabled = true, ShowMobileDownloadLink = true
        });
        context.RestaurantContracts.Add(new RestaurantContract
        {
            Id = 1, RestaurantId = 1, ContractNumber = "C1", PaymentPolicyId = 1,
            StatusId = StatusIds.Contract(ContractStatus.Active)
        });
        context.UserRestaurants.Add(new UserRestaurant
        {
            Id = 1, UserId = 1, RestaurantId = 1, RoleId = (int)RoleCode.Manager, IsActive = true
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
