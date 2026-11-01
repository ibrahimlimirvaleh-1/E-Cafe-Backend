using ECafe.Application.Common.Outbox;
using ECafe.Application.Services.Realtime;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Infrastructure.Services.Realtime;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task RealtimeSignalsAreInvisibleBeforeCommitAndDisappearOnRollbackInPostgres()
    {
        await using var database = await Database.CreateAsync(1);
        await using (var write = database.Context(realtime: true))
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            (await write.Orders.SingleAsync(order => order.Id == 1001)).StatusId = 2003;
            await write.SaveChangesAsync();
            Assert.Single(await write.OutboxEvents.Where(item => item.EventType == OutboxEventTypes.OrderRealtimeRequested).ToListAsync());
            await using var reader = database.Context(realtime: true);
            var notifier = new Mock<IUserRealtimeNotifier>(MockBehavior.Strict);
            Assert.Equal(0, await new RealtimeOutboxProcessor(reader, notifier.Object, new(reader)).ProcessAsync(25));
            await transaction.RollbackAsync();
        }
        await using var verify = database.Context();
        Assert.Empty(await verify.OutboxEvents.ToListAsync());
        Assert.Equal(2002, (await verify.Orders.SingleAsync(order => order.Id == 1001)).StatusId);
    }

    [StockPostgresFact]
    public async Task RealtimeSignalCommitsAtomicallyAndConcurrentWorkersDeliverOnlyOneClaimInPostgres()
    {
        await using var database = await Database.CreateAsync(1);
        await using (var write = database.Context(realtime: true))
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            (await write.Orders.SingleAsync(order => order.Id == 1001)).StatusId = 2003;
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifier = new Mock<IUserRealtimeNotifier>();
        notifier.Setup(service => service.NotifyOrderChangedAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<OrderChangedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { entered.TrySetResult(); await release.Task; });
        await using var first = database.Context(realtime: true);
        await using var second = database.Context(realtime: true);
        var processing = new RealtimeOutboxProcessor(first, notifier.Object, new(first)).ProcessAsync(25);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, await new RealtimeOutboxProcessor(second, notifier.Object, new(second)).ProcessAsync(25));
        }
        finally { release.TrySetResult(); }
        Assert.Equal(1, await processing);
        notifier.Verify(service => service.NotifyOrderChangedAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<OrderChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = database.Context();
        Assert.NotNull((await verify.OutboxEvents.SingleAsync()).ProcessedAt);
        Assert.Equal(2003, (await verify.Orders.SingleAsync(order => order.Id == 1001)).StatusId);
    }

    [StockPostgresFact]
    public async Task RealPreparingFlowPersistsStockAndRealtimeTogetherAndReplayDoesNotDuplicateInPostgres()
    {
        await using var database = await Database.CreateAsync(1);
        await using (var write = database.Context(realtime: true)) await Service(write).StartPreparingAsync(1001, 1001);
        await using (var replay = database.Context(realtime: true)) await Service(replay).StartPreparingAsync(1001, 1001);
        await using var verify = database.Context();
        Assert.Equal(0.5m, (await verify.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await verify.OutboxEvents.Where(item => item.EventType == OutboxEventTypes.OrderRealtimeRequested).ToListAsync());
    }

    [StockPostgresFact]
    public async Task FailedStockGuardPersistsNeitherPreparingNorRealtimeInPostgres()
    {
        await using var database = await Database.CreateAsync(0.1m);
        await using (var write = database.Context(realtime: true))
            await Assert.ThrowsAsync<ECafe.Domain.Exceptions.BusinessRuleException>(() => Service(write).StartPreparingAsync(1001, 1001));
        await using var verify = database.Context();
        Assert.Empty(await verify.OutboxEvents.ToListAsync());
        Assert.Equal(2002, (await verify.Orders.SingleAsync(order => order.Id == 1001)).StatusId);
        Assert.Equal(0.1m, (await verify.InventoryItems.SingleAsync()).QuantityOnHand);
    }
}
