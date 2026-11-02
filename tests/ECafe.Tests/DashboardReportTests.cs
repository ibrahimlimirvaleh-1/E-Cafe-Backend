using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Application.Services.Dashboard.Concrete;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class DashboardReportTests
{
    [Theory]
    [InlineData("orders", 2009)]
    [InlineData("orders", 2007)]
    [InlineData("reservations", 1003)]
    public void KnownReportStatusFiltersAreAccepted(string kind, int status)
        => DashboardReportPolicy.Resolve(RoleCode.Owner, kind, status, "all");

    [Theory]
    [InlineData("unknown", null, "all")]
    [InlineData("orders", 9999, "all")]
    [InlineData("orders", null, "refund")]
    [InlineData("reservations", 2009, "all")]
    [InlineData("payments", 3002, "all")]
    [InlineData("payments", null, "failed")]
    public void InvalidKindsAndUnrelatedFiltersAreRejected(string kind, int? status, string paymentEvent)
        => Assert.Throws<BadRequestException>(() => DashboardReportPolicy.Resolve(RoleCode.Owner, kind, status, paymentEvent));

    [Fact]
    public async Task ManagerCannotReadOrExportFinancialReportEvenWithForgedKind()
    {
        var access = new Mock<IDashboardRepository>();
        var reports = new Mock<IDashboardReportRepository>();
        var manager = Service(access.Object, reports.Object, RoleCode.Manager);
        var request = new DashboardReportRequest { RestaurantId = 1, Kind = "payments" };
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetAsync(RoleCode.Manager, request, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.ExportAsync(RoleCode.Manager, request, default));
        access.VerifyNoOtherCalls();
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeniedLiveAssignmentNeverLoadsReportData()
    {
        var reports = new Mock<IDashboardReportRepository>();
        var owner = Service(Mock.Of<IDashboardRepository>(), reports.Object, RoleCode.Owner);
        await Assert.ThrowsAsync<ForbiddenException>(() => owner.GetAsync(RoleCode.Owner,
            new() { RestaurantId = 99 }, default));
        reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportAndExportUseSameScopeAndDoNotDownloadOnlyTheCurrentPage()
    {
        var access = new Mock<IDashboardRepository>();
        access.Setup(repository => repository.ResolveAccessAsync(It.IsAny<DashboardScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardAccess([], false, "Asia/Baku"));
        var reports = new Mock<IDashboardReportRepository>();
        reports.Setup(repository => repository.GetReportAsync(It.IsAny<DashboardScope>(), It.IsAny<DashboardPeriod>(),
                It.IsAny<DashboardReportKind>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardReportData(new() { RowCount = 24, PendingCount = 2, ConfirmedAmount = 70 }, [], [], []));
        var owner = Service(access.Object, reports.Object, RoleCode.Owner);
        var request = new DashboardReportRequest { RestaurantId = 1, Kind = "orders", PageIndex = 2, PaymentMode = "test" };
        var result = await owner.GetAsync(RoleCode.Owner, request, default);
        Assert.Equal(24, result.TotalRows);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(70, Assert.Single(result.Metrics, metric => metric.Key == "confirmedAmount").Value);
        Assert.All(result.Metrics, metric => Assert.Null(metric.ChangePercent));
        var file = await owner.ExportAsync(RoleCode.Owner, request, default);
        Assert.StartsWith("ecafe-orders-", file.FileName);
        Assert.DoesNotContain("-test.csv", file.FileName);
        reports.Verify(repository => repository.GetReportAsync(It.Is<DashboardScope>(scope => scope.UserId == 2 &&
                scope.Role == RoleCode.Owner && scope.RestaurantId == 1 && scope.IsTest), It.IsAny<DashboardPeriod>(),
            DashboardReportKind.Orders, null, "all", 2, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ManagerReportMetricsExcludeMoneyAndCashFlowKeepsNegativeNet()
    {
        var totals = new DashboardReportTotals { RowCount = 3, ConfirmedAmount = 50, CollectedAmount = 10, RefundedAmount = 30 };
        Assert.DoesNotContain(DashboardReportMetricMapper.Map(DashboardReportKind.Orders, RoleCode.Manager, totals), metric => metric.Unit == "AZN");
        Assert.Equal(-20, Assert.Single(DashboardReportMetricMapper.Map(DashboardReportKind.Payments, RoleCode.Owner, totals),
            metric => metric.Key == "netCollectedAmount").Value);
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+cmd")]
    [InlineData("-cmd")]
    [InlineData("@SUM(A1)")]
    [InlineData("\t\r=cmd")]
    [InlineData("\uFEFF=cmd")]
    public void CsvNeutralizesSpreadsheetFormulasInUntrustedNames(string restaurant)
    {
        var report = Report(restaurant);
        var bytes = DashboardReportCsvMapper.Map(report).Content;
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'" + restaurant.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"", csv);
    }

    [Fact]
    public void CsvPreservesQuotedNamesNumbersAndUtcWithoutPrivateFields()
    {
        var file = DashboardReportCsvMapper.Map(Report("Tea, \"Coffee\"\nBaku"));
        var csv = Encoding.UTF8.GetString(file.Content);
        Assert.Contains("\"Tea, \"\"Coffee\"\"\nBaku\"", csv);
        Assert.Contains("\"12.50\"", csv);
        Assert.Contains("Asia/Baku", csv);
        Assert.Contains("not_applicable", csv);
        Assert.Contains("2026-11-02T10:00:00.0000000Z", csv);
        Assert.DoesNotContain("ProviderRef", csv);
        var json = JsonSerializer.Serialize(Report("Restaurant"));
        Assert.DoesNotContain("CustomerName", json);
        Assert.DoesNotContain("Phone", json);
        Assert.DoesNotContain("Note", json);
    }

    [Fact]
    public void AllReportAndExportEndpointsInheritPermissionAndNoStore()
    {
        Assert.NotNull(typeof(DashboardController).GetCustomAttribute<HasPermissionAttribute>());
        Assert.True(typeof(DashboardController).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        var methods = typeof(DashboardController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(6, methods.Count(method => method.Name.Contains("Report", StringComparison.Ordinal)));
        Assert.All(methods, method => Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>()));
    }

    [Fact]
    public void ReportIndexMigrationOnlyAddsFourNonUniqueIndexesAndKeepsExistingIndexes()
    {
        var migration = new ECafe.Infrastructure.Migrations.AddDashboardReportIndexes();
        var builder = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        Assert.Equal(4, builder.Operations.Count);
        Assert.All(builder.Operations, operation =>
            Assert.False(Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>(operation).IsUnique));
    }

    /// <summary>Sabit saat və token rolunu istifadə edən tətbiq xidmətini yalnız oxuma repository əvəzediciləri ilə qurur.</summary>
    private static DashboardReportManager Service(IDashboardRepository access, IDashboardReportRepository reports, RoleCode role)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", "2"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http);
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));
        return new(accessor, Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), access, reports, clock.Object);
    }

    /// <summary>CSV məzmunu və təhlükəsiz mətn kodlanması üçün sadə, şəxsi məlumatı olmayan hesabat yaradır.</summary>
    private static DashboardReportResponse Report(string restaurant)
        => new("orders", new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc), "Asia/Baku", new(2026, 11, 2), new(2026, 11, 2),
            "real", [], [new(1, 1, restaurant, "Masa 1", 1, 2, 2005, new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc), null, null, 1, 12.50m, 12.50m)],
            [], [], 1, 20, 1);
}
