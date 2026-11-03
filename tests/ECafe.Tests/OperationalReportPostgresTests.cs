using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Repositories.Dashboard;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed partial class DashboardPostgresTests
{
    [DashboardPostgresFact]
    public async Task OperationalReportsRunRealSqlAndKeepSnapshotPricesDurationsAndScope()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var order = await db.Orders.SingleAsync(row => row.Id == 1001);
        order.StatusId = 2005; order.ServedAt = Now.AddMinutes(5); order.AcceptedAt = Now.AddMinutes(-15); order.PreparingAt = Now.AddMinutes(-12);
        (await db.Orders.SingleAsync(row => row.Id == 1002)).CancelledAt = Now;
        var reservation = await db.Reservations.SingleAsync(row => row.Id == 1001);
        reservation.ArrivedAt = Now.AddMinutes(3);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        foreach (var kind in new[] { DashboardReportKind.Products, DashboardReportKind.Kitchen, DashboardReportKind.Cancellations,
            DashboardReportKind.ReservationOutcomes, DashboardReportKind.Tables, DashboardReportKind.Settlements, DashboardReportKind.Branches, DashboardReportKind.Stock })
        {
            var result = await repository.GetReportAsync(scope, period, kind, null, "all", 1, false, default);
            Assert.NotNull(result.Detail);
            Assert.All(result.Detail.Rows, row => Assert.Equal(result.Detail.Columns.Count, row.Cells.Count));
            Assert.DoesNotContain(result.Detail.Rows.SelectMany(row => row.Cells), cell => cell.Text == "Other");
        }
        var product = await repository.GetReportAsync(scope, period, DashboardReportKind.Products, null, "all", 1, false, default);
        var sold = product.Detail!.Rows.Single(row => row.Cells[1].Text == "Meal");
        Assert.Equal(2, sold.Cells[2].Number); Assert.Equal(50, sold.Cells[4].Number);
        Assert.Null(sold.Cells[6].Number);
        Assert.Null(sold.Cells[7].Number);
        Assert.Null(sold.Cells[8].Number);
        var kitchen = await repository.GetReportAsync(scope, period, DashboardReportKind.Kitchen, null, "all", 1, false, default);
        var ticket = Assert.Single(kitchen.Detail!.Rows);
        Assert.Equal(5, ticket.Cells[5].Number); Assert.Equal(12, ticket.Cells[6].Number); Assert.Equal(5, ticket.Cells[7].Number); Assert.Equal(25, ticket.Cells[8].Number);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    /// <summary>Sahibkarın maya və qazancı satış snapshot-ından aldığını, başqa rollara isə bu sütunların çıxmadığını yoxlayır.</summary>
    [DashboardPostgresFact]
    public async Task ProductProfitIsCalculatedFromSavedCostsForOwnerOnly()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var order = await db.Orders.Include(row => row.OrderItems).SingleAsync(row => row.Id == 1001);
        order.StatusId = 2005;
        order.ServedAt = Now;
        Assert.Single(order.OrderItems).IngredientCostSnapshot = .25m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var owner = await repository.GetReportAsync(scope, period, DashboardReportKind.Products, null, "all", 1, false, default);
        var row = owner.Detail!.Rows.Single(result => result.Cells[1].Text == "Meal");
        Assert.Equal(new[] { "ingredientCost", "grossProfit", "averageIngredientCost" }, owner.Detail.Columns.Skip(6).Select(column => column.Key));
        Assert.Equal(50m, row.Cells[4].Number);
        Assert.Equal(.25m, row.Cells[6].Number);
        Assert.Equal(49.75m, row.Cells[7].Number);
        Assert.Equal(.125m, row.Cells[8].Number);

        var manager = await repository.GetReportAsync(new(1002, RoleCode.Manager, 1001, false), period,
            DashboardReportKind.Products, null, "all", 1, true, default);
        Assert.Equal(6, manager.Detail!.Columns.Count);
    }

    [DashboardPostgresFact]
    public async Task StockAndCancellationUseDifferentTimeBasesAndNeverSumDifferentUnits()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var order = await db.Orders.SingleAsync(row => row.Id == 1001);
        order.StatusId = 2007; order.CancelledAt = Now; order.PreparingAt = Now.AddDays(-1); order.CancellationReason = "Unused ingredients";
        db.InventoryMovements.AddRange(
            new InventoryMovement { Id = 5001, RestaurantId = 1001, InventoryItemId = 1001, UnitId = 1, MovementTypeId = 4,
                RelatedOrderId = 1001, RelatedOrderItemId = 1001, QuantityChange = -1.2m, CreatedAt = Now.AddDays(-1) },
            new InventoryMovement { Id = 5002, RestaurantId = 1001, InventoryItemId = 1001, UnitId = 1, MovementTypeId = 6,
                RelatedOrderId = 1001, RelatedOrderItemId = 1001, ReversedMovementId = 5001, QuantityChange = .5m, CreatedAt = Now },
            new InventoryMovement { Id = 5003, RestaurantId = 1001, InventoryItemId = 1001, UnitId = 1, MovementTypeId = 5, QuantityChange = -.2m, CreatedAt = Now },
            new InventoryMovement { Id = 5004, RestaurantId = 1001, InventoryItemId = 1001, UnitId = 2, MovementTypeId = 1, QuantityChange = 100, CreatedAt = Now });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        // Fixture tarixlərini audit saatından ayırır; yalnız bu testin ayrıca bazasında işləyir.
        await db.InventoryMovements.Where(m => m.Id >= 5001 && m.Id <= 5004).ExecuteUpdateAsync(update => update.SetProperty(m => m.CreatedAt, Now));
        await db.InventoryMovements.Where(m => m.Id == 5001).ExecuteUpdateAsync(update => update.SetProperty(m => m.CreatedAt, Now.AddDays(-1)));
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var stock = await repository.GetReportAsync(scope, period, DashboardReportKind.Stock, null, "all", 1, false, default);
        var row = Assert.Single(stock.Detail!.Rows);
        Assert.Equal(1, row.Cells[3].Number); Assert.Equal(.5m, row.Cells[5].Number); Assert.Equal(.2m, row.Cells[6].Number);
        Assert.Equal(0, row.Cells[7].Number); Assert.Equal(.5m, row.Cells[8].Number); Assert.Equal(.2m, row.Cells[9].Number); Assert.Equal("yes", row.Cells[11].Text);
        var cancelled = await repository.GetReportAsync(scope, period, DashboardReportKind.Cancellations, null, "all", 1, false, default);
        var ingredient = Assert.Single(cancelled.Detail!.Rows);
        Assert.Equal(1.2m, ingredient.Cells[9].Number); Assert.Equal(.5m, ingredient.Cells[10].Number); Assert.Equal(.7m, ingredient.Cells[11].Number);
        Assert.Equal(1, await db.InventoryItems.Where(item => item.Id == 1001).Select(item => item.QuantityOnHand).SingleAsync());
    }

    [DashboardPostgresFact]
    public async Task StockExportIsAllFilteredRowsAndManagerCannotForgeFinancialKinds()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        for (var i = 0; i < 24; i++) db.InventoryItems.Add(new InventoryItem { Id = 6000 + i, RestaurantId = 1001, UnitId = 1, Name = "Ingredient " + i, IsActive = true, QuantityOnHand = 10 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1002, RoleCode.Manager, 1001, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var page = await repository.GetReportAsync(scope, period, DashboardReportKind.Stock, null, "all", 2, false, default);
        var export = await repository.GetReportAsync(scope, period, DashboardReportKind.Stock, null, "all", 2, true, default);
        Assert.Equal(5, page.Detail!.Rows.Count); Assert.Equal(25, export.Detail!.Rows.Count);
        await Assert.ThrowsAsync<ECafe.Application.Common.Exceptions.ForbiddenException>(() => repository.GetReportAsync(scope, period, DashboardReportKind.Settlements, null, "all", 1, false, default));
    }

    [DashboardPostgresFact]
    public async Task CurrentUnpaidBalanceUsesBillingPolicyAndPaymentIssuesKeepModesSeparate()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == 1002);
        payment.PaymentStatusId = 3003; payment.PaidAt = null;
        var refund = await db.ReservationRefunds.SingleAsync(r => r.Id == 1001);
        refund.StatusId = 8004; refund.RefundedAt = null;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await db.Payments.Where(p => p.Id == 1002).ExecuteUpdateAsync(update => update.SetProperty(p => p.CreatedAt, Now));
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var tables = await repository.GetReportAsync(scope, period, DashboardReportKind.Tables, null, "all", 1, false, default);
        Assert.Equal(40, Assert.Single(tables.Detail!.Rows).Cells[7].Number);
        var issues = await repository.GetReportAsync(scope, period, DashboardReportKind.Settlements, null, "all", 1, false, default);
        Assert.Equal(2, issues.Detail!.Rows.Count);
        Assert.Contains(issues.Detail.Rows, row => row.Cells[4].Text == "payment.3003");
        Assert.Contains(issues.Detail.Rows, row => row.Cells[4].Text == "refund.8004");
        Assert.DoesNotContain(issues.Detail.Rows.SelectMany(row => row.Cells), cell => cell.Text?.Contains("dash-", StringComparison.Ordinal) == true);
        var test = await repository.GetReportAsync(scope with { IsTest = true }, period, DashboardReportKind.Settlements, null, "all", 1, false, default);
        Assert.Empty(test.Detail!.Rows);
    }
}
