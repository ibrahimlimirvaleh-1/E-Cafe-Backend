using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Features.MobileApp;
using ECafe.Application.Features.Commands.Notification.Create;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Notification.Concrete;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Repositories.Notification;
using ECafe.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task KitchenStagesPersistCustomerInboxAndPushOutboxWithoutEmailOrDuplicates()
    {
        await using var fixture = await Database.CreateAsync(1m);
        await using (var setup = fixture.Context())
        {
            setup.Users.Add(new User { Id = 1003, RoleId = (int)RoleCode.Customer, IsActive = true,
                Name = "Test", Surname = "Customer", Email = "customer@example.test", Phone = "1003", Password = "test" });
            setup.TableSessions.Add(new TableSession { Id = 1001, RestaurantId = 1001, TableId = 1001,
                CustomerUserId = 1003, WaiterUserId = 1001, OpenedAt = DateTime.UtcNow,
                StatusId = StatusIds.TableSession(TableSessionStatus.Open) });
            var order = await setup.Orders.SingleAsync(o => o.Id == 1001);
            order.TableSessionId = 1001;
            order.CustomerUserId = 1003;
            order.StatusId = StatusIds.Build(ECafe.Domain.Enums.StatusType.Order, (int)OrderStatus.Created);
            order.AcceptedAt = null;
            order.AcceptedByUserId = null;
            await setup.SaveChangesAsync();
        }

        var mapper = new MapperConfiguration(config => config.AddProfile<NotificationProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:PushDeliveryReady"] = "true",
            ["MobileApp:ExpoProjectId"] = Guid.NewGuid().ToString()
        }).Build();
        foreach (var stage in new[] { OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready })
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var db = fixture.Context();
                var notices = new NotificationManager(Accessor(), mapper, configuration, new NotificationRepository(db),
                    new CreateNotificationRequestValidator(), Mock.Of<INotificationChangeDispatcher>(),
                    new MobilePushOutboxWriter(db, configuration), new NotificationEmailOutboxWriter(db));
                var kitchen = Service(db, notifications: notices);
                if (stage == OrderStatus.Accepted) await kitchen.AcceptAsync(1001, 1001);
                else if (stage == OrderStatus.Preparing) await kitchen.StartPreparingAsync(1001, 1001);
                else await kitchen.MarkReadyAsync(1001, 1001);
            }
        }

        await using var read = fixture.Context();
        var persisted = await read.Notifications.OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(4, persisted.Count);
        Assert.Equal((int)NotificationType.OrderReady, Assert.Single(persisted, n => n.UserId == 1001).TypeId);
        Assert.Equal(new[] { NotificationType.CustomerOrderAccepted, NotificationType.CustomerOrderPreparing,
            NotificationType.CustomerOrderReady }, persisted.Where(n => n.UserId == 1003).Select(n => (NotificationType)n.TypeId));
        var outbox = await read.OutboxEvents.Where(e => e.AggregateType == AuditEntityTypes.Order && e.AggregateId == 1001).ToListAsync();
        var pushes = outbox.Where(e => e.EventType == OutboxEventTypes.MobilePushRequested)
            .Select(e => JsonSerializer.Deserialize<MobilePushOutboxPayload>(e.Payload)!).ToArray();
        Assert.Equal(4, pushes.Length);
        Assert.Equal(3, pushes.Count(p => p.UserId == 1003 && p.RestaurantId == 1001 && p.RelatedEntityId == 1001));
        var email = Assert.Single(outbox, e => e.EventType == OutboxEventTypes.EmailNotificationRequested);
        Assert.DoesNotContain("customer@example.test", email.Payload);
    }
}
