using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task PartialMigrationPreservesLegacyRequestsAndAllowsTheNewDecision()
    {
        await using var f = await PreparedCancellationAsync();
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE ops.order_cancellation_stock_return_lines; ALTER TABLE ops.order_cancellation_requests DROP CONSTRAINT cancellation_stock_return_mode_ck; ALTER TABLE ops.order_cancellation_requests DROP COLUMN \"StockReturnMode\";");
        var migration = new ECafe.Infrastructure.Migrations.AddPartialCancellationStockReturn();
        var sql = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in sql.Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Null((await db.OrderCancellationRequests.SingleAsync()).StockReturnMode);
        Assert.Empty(await db.Set<OrderCancellationStockReturnLine>().ToListAsync());
        await RequestService(db, true).DecideAsync(1001, 1001, Partial(0.1m));
        Assert.Equal(0.6m, (await db.InventoryItems.SingleAsync()).QuantityOnHand);
    }

    [StockPostgresFact]
    public async Task PartialReturnUsesOriginalSourceAmountsAndPersistsOneFinalPlan()
    {
        await using var f = await PreparedCancellationAsync();
        await using var db = f.Context();
        var service = RequestService(db, true);
        var preview = await service.GetStockReturnPreviewAsync(1001, 1001);
        Assert.Equal(0.5m, Assert.Single(preview.Ingredients).ConsumedQuantity);
        Assert.Equal(0.5m, (await db.InventoryItems.AsNoTracking().SingleAsync()).QuantityOnHand);
        var recipe = await db.Recipes.SingleAsync();
        recipe.Quantity = 9999;
        recipe.IsActive = false;
        await db.SaveChangesAsync();
        var first = await service.DecideAsync(1001, 1001, Partial(0.123456m));
        Assert.Equal(OrderStockReturnMode.ByIngredient, first.CancellationRequest!.StockReturnMode);
        Assert.Null(first.CancellationRequest.IngredientsUnused);
        Assert.NotNull(first.CancellationRequest.StockReturnedAt);
        Assert.Equal(0.123456m, Assert.Single(first.StockReturns!).ReturnQuantity);
        var again = await service.DecideAsync(1001, 1001, Partial(0.123456m) with { Reason = "Təkrar səbəb" });
        Assert.Equal(first.CancellationRequest, again.CancellationRequest);
        await using var read = f.Context();
        Assert.Equal(0.623456m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        var line = Assert.Single(await read.Set<OrderCancellationStockReturnLine>().ToListAsync());
        Assert.Equal(0.5m, line.ConsumedQuantity);
        Assert.Equal(0.123456m, line.ReturnQuantity);
        Assert.Equal(2, await read.InventoryMovements.CountAsync());
        var customer = await CustomerOrderTrackingMapper.ProjectOrders(read.Orders.Where(order => order.Id == 1001)).SingleAsync();
        Assert.DoesNotContain("inventoryItemId", JsonSerializer.Serialize(customer, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Empty(await read.Payments.ToListAsync());
    }

    [StockPostgresFact]
    public async Task PartialZeroIsAFinalCancellationWithoutStockMovementOrReturnTimestamp()
    {
        await using var f = await PreparedCancellationAsync();
        await using var db = f.Context();
        var result = await RequestService(db, true).DecideAsync(1001, 1001, Partial(0));
        Assert.Equal(2007, result.StatusId);
        Assert.Null(result.CancellationRequest!.StockReturnedAt);
        Assert.Single(await db.InventoryMovements.ToListAsync());
        Assert.Equal(0.5m, (await db.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(0m, (await db.Set<OrderCancellationStockReturnLine>().SingleAsync()).ReturnQuantity);
        Assert.Equal(ErrorCode.OrderCancellationRequestConflict, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RequestService(db, true).DecideAsync(1001, 1001, Partial(0.1m)))).Code);
    }

    [StockPostgresFact]
    public async Task ParallelIdenticalPartialPlansRestoreStockAndAuditOnce()
    {
        await using var f = await PreparedCancellationAsync();
        await using var a = f.Context();
        await using var b = f.Context();
        var audit = new Mock<IAuditLogService>();
        var results = await Task.WhenAll(RequestService(a, true, audit).DecideAsync(1001, 1001, Partial(0.2m)),
            RequestService(b, true, audit).DecideAsync(1001, 1001, Partial(0.2m)));
        Assert.Equal(results[0].CancellationRequest, results[1].CancellationRequest);
        await using var read = f.Context();
        Assert.Equal(0.7m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Single(await read.Set<OrderCancellationStockReturnLine>().ToListAsync());
        Assert.Equal(2, await read.InventoryMovements.CountAsync());
        Assert.Single(audit.Invocations, call => Equals(call.Arguments[1], AuditActions.OrderCancellationApproved));
    }

    [StockPostgresFact]
    public async Task ParallelDifferentPartialPlansCannotOverwriteTheWinningQuantities()
    {
        await using var f = await PreparedCancellationAsync();
        await using var a = f.Context();
        await using var b = f.Context();
        var results = await Task.WhenAll(TryPartialAsync(a, 0.1m), TryPartialAsync(b, 0.2m));
        Assert.Single(results, error => error == null);
        Assert.Equal(ErrorCode.OrderCancellationRequestConflict, Assert.Single(results.OfType<BusinessRuleException>()).Code);
        await using var read = f.Context();
        var line = await read.Set<OrderCancellationStockReturnLine>().SingleAsync();
        Assert.Equal(0.5m + line.ReturnQuantity, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(2, await read.InventoryMovements.CountAsync());
    }

    [StockPostgresFact]
    public async Task PartialReturnFailureRollsBackTheDecisionLedgerAndIngredientLines()
    {
        await using var f = await PreparedCancellationAsync();
        await using (var write = f.Context())
        {
            var notifications = new Mock<INotificationService>();
            notifications.Setup(service => service.CreateAsync(It.IsAny<ECafe.Application.DTOs.Notification.CreateNotificationRequest>()))
                .ThrowsAsync(new InvalidOperationException("Outbox unavailable"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => RequestService(write, true, notifications: notifications)
                .DecideAsync(1001, 1001, Partial(0.2m)));
        }
        await using var read = f.Context();
        Assert.Equal(2003, (await read.Orders.SingleAsync(order => order.Id == 1001)).StatusId);
        Assert.Null((await read.OrderCancellationRequests.SingleAsync()).StockReturnMode);
        Assert.Empty(await read.Set<OrderCancellationStockReturnLine>().ToListAsync());
        Assert.Single(await read.InventoryMovements.ToListAsync());
        Assert.Equal(0.5m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
    }

    [StockPostgresFact]
    public async Task AmountAboveOriginalConsumptionCannotCancelOrIncreaseStock()
    {
        await using var f = await PreparedCancellationAsync();
        await using (var write = f.Context())
            Assert.Equal(ErrorCode.OrderStockReturnQuantitiesInvalid, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                RequestService(write, true).DecideAsync(1001, 1001, Partial(0.500001m)))).Code);
        await using var read = f.Context();
        Assert.Null((await read.OrderCancellationRequests.SingleAsync()).ResolvedAt);
        Assert.Empty(await read.Set<OrderCancellationStockReturnLine>().ToListAsync());
        Assert.Equal(0.5m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
    }

    [StockPostgresFact]
    public async Task SharedIngredientPreviewAggregatesLinesAndPartialReturnAllocatesEachSourceAtMostOnce()
    {
        await using var f = await Database.CreateAsync(2);
        await AddCancellationSessionAsync(f);
        await using (var write = f.Context())
        {
            write.OrderItems.Add(new OrderItem { Id = 1100, OrderId = 1001, ItemId = 1001,
                NameSnapshot = "Extra meal", Quantity = 1, UnitPrice = 3, UnitPriceSnapshot = 3, LineTotal = 3 });
            await write.SaveChangesAsync();
            await Service(write).StartPreparingAsync(1001, 1001);
            await RequestService(write, false).RequestForWaiterAsync(1001, 1001, 1001, new("Plan dəyişdi"));
        }
        await using (var write = f.Context())
        {
            var preview = await RequestService(write, true).GetStockReturnPreviewAsync(1001, 1001);
            Assert.Equal(0.75m, Assert.Single(preview.Ingredients).ConsumedQuantity);
            await RequestService(write, true).DecideAsync(1001, 1001, Partial(0.6m));
        }
        await using var read = f.Context();
        Assert.Equal(1.85m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        var rows = await read.InventoryMovements.ToListAsync();
        Assert.Equal(2, rows.Count(row => row.MovementTypeId == 4));
        Assert.Equal(2, rows.Count(row => row.MovementTypeId == 6));
        foreach (var returned in rows.Where(row => row.MovementTypeId == 6))
            Assert.InRange(returned.QuantityChange, 0.000001m, -rows.Single(row => row.Id == returned.ReversedMovementId).QuantityChange);
    }

    [StockPostgresFact]
    public async Task PartialReturnAndManualDecreaseShareTheInventoryRowLock()
    {
        await using var f = await PreparedCancellationAsync();
        await using var a = f.Context();
        await using var b = f.Context();
        var decision = RequestService(a, true).DecideAsync(1001, 1001, Partial(0.2m));
        var manual = TryManualDecreaseAsync(b);
        await Task.WhenAll(decision, manual).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Null(await manual);
        await using var read = f.Context();
        Assert.Equal(0.2m, (await read.InventoryItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(3, await read.InventoryMovements.CountAsync());
    }

    [StockPostgresFact]
    public async Task PartialPlanIncludesEveryIngredientAndZeroReturnDoesNotIncreaseThatIngredient()
    {
        await using var f = await PreparedCancellationAsync();
        await using (var write = f.Context())
        {
            write.InventoryItems.Add(new InventoryItem { Id = 1100, RestaurantId = 1001, UnitId = 1, Name = "Water", QuantityOnHand = 0.9m, IsActive = true });
            write.InventoryMovements.Add(new InventoryMovement { RestaurantId = 1001, InventoryItemId = 1100, UnitId = 1,
                RelatedOrderId = 1001, RelatedOrderItemId = 1001, MovementTypeId = 4, QuantityChange = -0.1m });
            await write.SaveChangesAsync();
        }
        await using (var incomplete = f.Context())
            Assert.Equal(ErrorCode.OrderStockReturnQuantitiesInvalid, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                RequestService(incomplete, true).DecideAsync(1001, 1001, Partial(0.2m)))).Code);
        await using (var write = f.Context())
            await RequestService(write, true).DecideAsync(1001, 1001, Partial(0.2m) with { StockReturns = [new(1100, 0), new(1001, 0.2m)] });
        await using var read = f.Context();
        Assert.Equal(0.9m, (await read.InventoryItems.SingleAsync(item => item.Id == 1100)).QuantityOnHand);
        Assert.Equal(0.7m, (await read.InventoryItems.SingleAsync(item => item.Id == 1001)).QuantityOnHand);
        Assert.Equal(2, await read.Set<OrderCancellationStockReturnLine>().CountAsync());
        Assert.Single(await read.InventoryMovements.Where(row => row.MovementTypeId == 6).ToListAsync());
    }

    [StockPostgresFact]
    public async Task IngredientPreviewCannotBeReadByWaiterOrAnotherRestaurant()
    {
        await using var f = await PreparedCancellationAsync();
        await using var db = f.Context();
        await Assert.ThrowsAsync<ECafe.Application.Common.Exceptions.ForbiddenException>(() =>
            RequestService(db, false).GetStockReturnPreviewAsync(1001, 1001));
        await Assert.ThrowsAsync<ECafe.Application.Common.Exceptions.ForbiddenException>(() =>
            RequestService(db, true).GetStockReturnPreviewAsync(1002, 1001));
    }

    /// <summary>Qismən planın normalizə olunmuş müqaviləsini yalnız test olunan ərzaq və anbar vahidində yaradır.</summary>
    private static DecideOrderCancellationRequest Partial(decimal quantity)
        => new(true, "Ərzaq miqdarı yoxlandı", null, OrderStockReturnMode.ByIngredient, [new(1001, quantity)]);

    /// <summary>Paralel qərarın nəticəsini müqayisə üçün saxlayır; müstəqil DB kontekstləri real masa kilidində yarışır.</summary>
    private static async Task<Exception?> TryPartialAsync(ECafe.Infrastructure.Context.ECafeDbContext db, decimal quantity)
    {
        try { await RequestService(db, true).DecideAsync(1001, 1001, Partial(quantity)); return null; }
        catch (BusinessRuleException exception) { return exception; }
    }
}
