using System.Data.Common;
using System.Text.Json;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Repositories.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ECafe.Tests;

public sealed partial class DashboardPostgresTests
{
    [DashboardPostgresFact]
    public async Task ReportSqlKeepsPendingQuotesOutOfConfirmedDashboardAmounts()
    {
        await using var f = await Database.CreateAsync();
        await using (var seed = f.Context())
        {
            seed.Orders.Add(new Order { Id = 1004, RestaurantId = 1001, TableId = 1001, SourceId = 1,
                StatusId = 2009, OpenedAt = Now, OrderItems = [new OrderItem { ItemId = 1001, NameSnapshot = "Pending",
                    Quantity = 1, UnitPrice = 123, UnitPriceSnapshot = 123, LineTotal = 123 }] });
            await seed.SaveChangesAsync();
        }
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var report = await repository.GetReportAsync(scope, period, DashboardReportKind.Orders, null, "all", 1, false, default);
        Assert.Equal(3, report.Totals.RowCount);
        Assert.Equal(1, report.Totals.PendingCount);
        Assert.Equal(1, report.Totals.CancelledCount);
        Assert.Equal(50, report.Totals.ConfirmedAmount);
        Assert.All(report.Orders, row => Assert.Equal(1001, row.RestaurantId));
        Assert.Equal(123, report.Orders.Single(row => row.StatusId == 2009).ItemSubtotal);
        Assert.Equal(0, report.Orders.Single(row => row.StatusId == 2009).ConfirmedAmount);
        Assert.Equal(0, report.Orders.Single(row => row.StatusId == 2007).ConfirmedAmount);
        var totals = await repository.GetTotalsAsync(scope, period.StartUtc, period.EndUtc, default);
        Assert.Equal(1, totals.OrderCount);
        Assert.Equal(50, totals.OrderAmount);
        Assert.Equal(50, (await repository.GetChartAsync(scope, period, default)).Sum(point => point.Value));
        Assert.Equal(50, Assert.Single((await repository.GetBranchesAsync(scope, period, Now, 1, default)).Rows).OrderAmount);
        var pending = await repository.GetReportAsync(scope, period, DashboardReportKind.Orders, 2009, "all", 1, false, default);
        Assert.Single(pending.Orders);
        Assert.Equal(0, pending.Totals.ConfirmedAmount);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [DashboardPostgresFact]
    public async Task ReportSqlSeparatesReceiptAndRefundDatesModesAndPrivateData()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var report = await repository.GetReportAsync(scope, period, DashboardReportKind.Payments, null, "all", 1, false, default);
        Assert.Equal(3, report.Totals.RowCount);
        Assert.Equal(50, report.Totals.CollectedAmount);
        Assert.Equal(10, report.Totals.RefundedAmount);
        Assert.Equal(2, report.Totals.ReceiptCount);
        Assert.Equal(1, report.Totals.RefundCount);
        Assert.All(report.Payments, row => { Assert.False(row.IsTest); Assert.Equal(1001, row.RestaurantId); });
        var test = await repository.GetReportAsync(scope with { IsTest = true }, period, DashboardReportKind.Payments, null, "all", 1, false, default);
        Assert.Equal(200, test.Totals.CollectedAmount);
        Assert.True(Assert.Single(test.Payments).IsTest);
        var refund = await repository.GetReportAsync(scope, period, DashboardReportKind.Payments, null, "refund", 1, false, default);
        Assert.Equal("refund", Assert.Single(refund.Payments).Event);
        Assert.Equal(0, refund.Totals.CollectedAmount);
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("dash-", json);
        Assert.DoesNotContain("DashboardTest", json);
        Assert.DoesNotContain("ProviderRef", json);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [DashboardPostgresFact]
    public async Task ManagerReservationReportIsScopedAndPaymentsAreRejected()
    {
        await using var f = await Database.CreateAsync();
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1002, RoleCode.Manager, 1001, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var report = await repository.GetReportAsync(scope, period, DashboardReportKind.Reservations, null, "all", 1, false, default);
        Assert.Equal(2, report.Reservations.Count);
        Assert.Equal(4, report.Totals.GuestCount);
        Assert.Equal(1, report.Totals.SeatedCount);
        Assert.All(report.Reservations, row => Assert.Equal(1001, row.RestaurantId));
        await Assert.ThrowsAsync<ForbiddenException>(() => repository.GetReportAsync(scope, period, DashboardReportKind.Payments, null, "all", 1, false, default));
        var filtered = await repository.GetReportAsync(scope, period, DashboardReportKind.Reservations, 1002, "all", 1, false, default);
        Assert.Equal(1002, Assert.Single(filtered.Reservations).StatusId);
        var foreign = await repository.GetReportAsync(scope with { RestaurantId = 1002 }, period, DashboardReportKind.Orders, null, "all", 1, false, default);
        Assert.Empty(foreign.Orders);
    }

    [DashboardPostgresFact]
    public async Task ExportReadsAllFilteredRowsAndRejectsRatherThanTruncatesAtLimit()
    {
        await using var f = await Database.CreateAsync();
        await using (var seed = f.Context())
        {
            seed.Orders.AddRange(Enumerable.Range(2000, 4998).Select(id => new Order { Id = id, RestaurantId = 1001,
                TableId = 1001, SourceId = 2, StatusId = 2001, OpenedAt = Now }));
            await seed.SaveChangesAsync();
        }
        await using var db = f.Context();
        var repository = new DashboardRepository(db);
        var scope = new DashboardScope(1001, RoleCode.Owner, null, false);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var page = await repository.GetReportAsync(scope, period, DashboardReportKind.Orders, null, "all", 2, false, default);
        Assert.Equal(20, page.Orders.Count);
        Assert.Equal(5000, page.Totals.RowCount);
        var export = await repository.GetReportAsync(scope, period, DashboardReportKind.Orders, null, "all", 2, true, default);
        Assert.Equal(5000, export.Orders.Count);
        await using (var seed = f.Context())
        {
            seed.Orders.Add(new Order { Id = 9000, RestaurantId = 1001, TableId = 1001, SourceId = 2, StatusId = 2001, OpenedAt = Now });
            await seed.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<BadRequestException>(() => repository.GetReportAsync(scope, period, DashboardReportKind.Orders, null, "all", 1, true, default));
        Assert.Equal(5001, (await repository.GetReportAsync(scope, period, DashboardReportKind.Orders, null, "all", 1, false, default)).Totals.RowCount);
    }

    [DashboardPostgresFact]
    public async Task ReportTotalsAndRowsShareSnapshotWhenStatusChangesBetweenQueries()
    {
        await using var f = await Database.CreateAsync();
        var interceptor = new ReportSnapshotInterceptor(async () =>
        {
            await using var writer = f.Context();
            await writer.Orders.Where(order => order.Id == 1001).ExecuteUpdateAsync(setters => setters.SetProperty(order => order.StatusId, 2007));
        });
        await using var db = f.Context(interceptor);
        var repository = new DashboardRepository(db);
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(Now));
        var report = await repository.GetReportAsync(new(1001, RoleCode.Owner, null, false), period,
            DashboardReportKind.Orders, 2004, "all", 1, false, default);
        Assert.Equal(1, report.Totals.RowCount);
        Assert.Equal(2004, Assert.Single(report.Orders).StatusId);
        await using var read = f.Context();
        Assert.Equal(2007, (await read.Orders.SingleAsync(order => order.Id == 1001)).StatusId);
    }

    private sealed class ReportSnapshotInterceptor(Func<Task> change) : DbCommandInterceptor
    {
        private int _changed;

        /// <summary>İlk aqreqat SELECT-dən sonra ayrıca bağlantıda status dəyişir; hesabatın ikinci sorğusunun eyni snapshot-da qalmasını yoxlayır.</summary>
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && Interlocked.Exchange(ref _changed, 1) == 0)
                await change();
            return result;
        }
    }
}
