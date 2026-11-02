using System.Security.Claims;
using System.Text;
using ECafe.Api.Middlewares;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Domain.Constants;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Tests;

public sealed class BusinessCodeRefactorTests
{
    [Fact]
    public void EveryReportHasExactlyOneStableDefinition()
    {
        Assert.Equal(Enum.GetValues<DashboardReportKind>().Length, DashboardReportCatalog.All.Count);
        Assert.Equal(DashboardReportCatalog.All.Count, DashboardReportCatalog.All.Select(row => row.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(DashboardReportCatalog.All.Count, DashboardReportCatalog.All.Select(row => row.Kind).Distinct().Count());
        foreach (var definition in DashboardReportCatalog.All)
        {
            Assert.True(DashboardReportCatalog.TryGet(definition.Code, out var byCode));
            Assert.Same(definition, byCode);
            Assert.Same(definition, DashboardReportCatalog.Get(definition.Kind));
            var financial = definition.Kind is DashboardReportKind.Payments or DashboardReportKind.Settlements or DashboardReportKind.Branches;
            Assert.Equal(financial, definition.UsesPaymentMode);
            Assert.Equal(!financial, definition.AllowsManager);
            if (financial) Assert.Throws<ECafe.Application.Common.Exceptions.ForbiddenException>(() => DashboardReportPolicy.Resolve(RoleCode.Manager, definition.Code, null, "all"));
            else Assert.Equal(definition.Kind, DashboardReportPolicy.Resolve(RoleCode.Manager, definition.Code, null, "all"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Orders")]
    [InlineData("new-unknown-report")]
    public void UnknownReportCodesDoNotDefaultToAnotherReport(string? code)
        => Assert.False(DashboardReportCatalog.TryGet(code, out _));

    [Fact]
    public void DefaultsAndRoutesRetainExistingPublicValues()
    {
        var request = new DashboardReportRequest();
        Assert.Equal("orders", request.Kind); Assert.Equal("week", request.Period);
        Assert.Equal("real", request.PaymentMode); Assert.Equal("all", request.PaymentEvent);
        Assert.Equal("AZN", new ECafe.Domain.Entities.Payment().CurrencyCode);
        Assert.Equal("AZN", new ECafe.Domain.Entities.ReservationRefund().CurrencyCode);
        Assert.Equal(CurrencyCodes.Azn, TableSessionBillingPolicy.Currency);
        Assert.Equal("Asia/Baku", new ECafe.Domain.Entities.Restaurant().TimeZone);
        Assert.Equal(RestaurantDefaults.TimeZone, DashboardPeriodPolicy.DefaultTimeZone);
        Assert.Equal("/api/v1/user", ApiRoutes.Auth.CookiePath);
        Assert.Equal("/api/v1/user/refresh", ApiRoutes.Auth.RefreshPath);
        Assert.Equal("/hubs/user-events", ApiRoutes.Realtime.UserEvents);
    }

    [Fact]
    public void CsvModeMetadataAndFilesAgreeForAllRegisteredReports()
    {
        foreach (var definition in DashboardReportCatalog.All)
        {
            var detail = new DashboardReportDetail([new("quantity", "number")], [new("1", [new(Number: .25m)])]);
            var report = new DashboardReportResponse(definition.Code, DateTime.UtcNow, "Asia/Baku", new(2026, 11, 1), new(2026, 11, 3),
                "test", [], [], [], [], 1, 20, 1, detail, 4);
            var file = DashboardReportCsvMapper.Map(report);
            Assert.Equal(definition.UsesPaymentMode, file.FileName.EndsWith("-test.csv", StringComparison.Ordinal));
            Assert.Equal(new byte[] { 239, 187, 191 }, file.Content.Take(3));
            var csv = Encoding.UTF8.GetString(file.Content);
            Assert.StartsWith("\uFEFF\"report_kind\",\"time_zone\",\"period_from\",\"period_to\",\"payment_mode\",\"day_start_hour\",", csv);
            if (definition.Kind is not (DashboardReportKind.Orders or DashboardReportKind.Reservations or DashboardReportKind.Payments))
                Assert.Contains($"\"{(definition.UsesPaymentMode ? "test" : "not_applicable")}\",\"4\",\"0.25\"\r\n", csv);
        }
    }

    [Theory]
    [InlineData("/api/v1/user/refresh")]
    [InlineData("/API/V1/USER/REFRESH")]
    public async Task RefreshExemptionStillUsesExactCaseInsensitivePath(string path)
    {
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity([new("userId", "1")], "test")) };
        context.Request.Path = path;
        var reachedNext = false;
        var middleware = new ActiveUserMiddleware(_ => { reachedNext = true; return Task.CompletedTask; });
        var cache = new Mock<IUserSessionStateCache>(MockBehavior.Strict);
        await middleware.Invoke(context, cache.Object);
        Assert.True(reachedNext); cache.VerifyNoOtherCalls();
        context.Request.Path = path + "-other";
        await Assert.ThrowsAsync<ECafe.Domain.Exceptions.UnauthorizedException>(() => middleware.Invoke(context, cache.Object));
    }
}
