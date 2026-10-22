using System.Security.Claims;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Application.Services.Dashboard.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class DashboardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TodayUsesRestaurantCalendarNotUtcCalendar()
    {
        var period = DashboardPeriodPolicy.Resolve("today", null, null, "Asia/Baku", new(2026, 10, 21, 22, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 22), period.From);
        Assert.Equal(new DateTime(2026, 10, 21, 20, 0, 0, DateTimeKind.Utc), period.StartUtc);
        Assert.Equal(period.StartUtc.AddDays(1), period.EndUtc);
        Assert.Equal(period.StartUtc.AddDays(-1), period.PreviousStartUtc);
        Assert.Equal(period.StartUtc, period.PreviousEndUtc);
    }

    [Theory]
    [InlineData("today", 1)]
    [InlineData("week", 7)]
    [InlineData("month", 30)]
    public void PresetsHaveEqualPreviousCalendarPeriods(string key, int days)
    {
        var period = DashboardPeriodPolicy.Resolve(key, null, null, "Asia/Baku", Now);
        Assert.Equal(days, period.Days);
        Assert.Equal(period.StartUtc - period.PreviousStartUtc, period.EndUtc - period.StartUtc);
    }

    [Fact]
    public void DaylightSavingRangeUsesCalendarDaysNotTwentyFourHourAssumption()
    {
        var period = DashboardPeriodPolicy.Resolve("custom", new(2026, 3, 29), new(2026, 3, 29), "Europe/Berlin", Now);
        Assert.Equal(23, (period.EndUtc - period.StartUtc).TotalHours);
        Assert.Equal(23, DashboardChartMapper.Map(RoleCode.Manager, period, []).Points.Count);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("custom")]
    [InlineData("")]
    public void InvalidPresetOrMissingCustomBoundsAreRejected(string key)
        => Assert.Throws<BadRequestException>(() => DashboardPeriodPolicy.Resolve(key, null, null, "Asia/Baku", Now));

    [Fact]
    public void InvalidCustomRangesAreRejected()
    {
        Assert.Throws<BadRequestException>(() => DashboardPeriodPolicy.Resolve("custom", new(2026, 10, 22), new(2026, 10, 21), "Asia/Baku", Now));
        Assert.Throws<BadRequestException>(() => DashboardPeriodPolicy.Resolve("custom", new(2026, 1, 1), new(2026, 12, 31), "Asia/Baku", Now));
    }

    [Fact]
    public void ZeroPreviousValueDoesNotInventOneHundredPercentGrowth()
    {
        Assert.Null(DashboardPeriodPolicy.ChangePercent(50, 0));
        Assert.Equal(25, DashboardPeriodPolicy.ChangePercent(125, 100));
        Assert.Equal(-100, DashboardPeriodPolicy.ChangePercent(0, 100));
    }

    [Fact]
    public void ChartCompletesMissingLocalDaysAndPutsUtcEveningInNextBakuDay()
    {
        var period = DashboardPeriodPolicy.Resolve("week", null, null, "Asia/Baku", Now);
        var chart = DashboardChartMapper.Map(RoleCode.Owner, period, [new(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc), 21, 50)]);
        Assert.Equal(7, chart.Points.Count);
        Assert.Equal(50, chart.Points[^1].Value);
        Assert.Equal(50, chart.Points.Sum(point => point.Value));
        Assert.Equal("AZN", chart.Unit);
    }

    [Theory]
    [InlineData("Asia/Kolkata", 18, 30)]
    [InlineData("Asia/Kathmandu", 18, 15)]
    public void HourChartKeepsHalfHourAndQuarterHourTimezoneBoundaries(string zone, int hour, int minute)
    {
        var period = DashboardPeriodPolicy.Resolve("today", null, null, zone, Now);
        var chart = DashboardChartMapper.Map(RoleCode.Owner, period,
            [new(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc), hour, 50, minute)]);
        Assert.Equal(50, chart.Points[0].Value);
        Assert.Equal(50, chart.Points.Sum(point => point.Value));
    }

    [Fact]
    public void ManagerMetricsAndAlertsDoNotExposePlatformMoneyOrUserCounts()
    {
        var totals = new DashboardTotals(10, 100, 20, 90, 10, 80, 5);
        var live = Live();
        var metrics = DashboardMetricMapper.Map(RoleCode.Manager, totals, totals, live);
        Assert.Equal(new[] { "todayReservations", "openSessions", "kitchenOrders", "readyOrders" }, metrics.Select(metric => metric.Key));
        Assert.All(metrics, metric => Assert.Null(metric.PreviousValue));
        Assert.DoesNotContain(DashboardAlertMapper.Map(RoleCode.Manager, live, 1), alert => alert.Key is "paymentReview" or "pendingRefunds" or "failedPayments");
    }

    [Fact]
    public void AlertsOmitZeroCountsAndOwnerAverageIsSafeOnEmptyData()
    {
        var empty = new DashboardTotals(0, 0, 0, 0, 0, 0, 0);
        Assert.Empty(DashboardAlertMapper.Map(RoleCode.Owner, new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), null));
        Assert.All(DashboardMetricMapper.Map(RoleCode.Owner, empty, empty, Live()), metric => Assert.Equal(0, metric.Value));
    }

    [Fact]
    public async Task RepositoryUsesLiveAssignmentsAndRejectsCrossRestaurantOrDeactivatedUser()
    {
        await using var db = Context();
        db.Users.Add(new() { Id = 101, Name = "Owner", Surname = "Test", Email = "owner@test", Phone = "101", Password = "test", RoleId = 2, IsActive = true });
        db.Restaurants.AddRange(new Restaurant { Id = 201, Name = "Own", Phone = "201", Location = "Baku", IsActive = true },
            new Restaurant { Id = 202, Name = "Other", Phone = "202", Location = "Baku", IsActive = true });
        db.UserRestaurants.Add(new() { Id = 301, UserId = 101, RestaurantId = 201, RoleId = 2, IsActive = true });
        await db.SaveChangesAsync();
        var repository = new DashboardRepository(db);
        var owner = new DashboardScope(101, RoleCode.Owner, null, false);
        Assert.Equal(201, Assert.Single((await repository.ResolveAccessAsync(owner, default))!.Restaurants).Id);
        Assert.Null(await repository.ResolveAccessAsync(owner with { RestaurantId = 202 }, default));
        Assert.Null(await repository.ResolveAccessAsync(owner with { Role = RoleCode.Manager }, default));
        Assert.Null(await repository.ResolveAccessAsync(owner with { Role = RoleCode.SuperAdmin }, default));
        var assignment = await db.UserRestaurants.SingleAsync();
        assignment.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await repository.ResolveAccessAsync(owner, default));
        assignment.IsActive = true;
        (await db.Users.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await repository.ResolveAccessAsync(owner, default));
    }

    [Fact]
    public async Task ManagerResponseHasNoFinancialSummaryOrBranchReport()
    {
        var repository = Repository();
        var service = Service(repository.Object, RoleCode.Manager);
        var result = await service.GetAsync(RoleCode.Manager, new() { RestaurantId = 201, Period = "today" }, default);
        Assert.Null(result.Financials);
        Assert.Empty(result.BranchRows);
        Assert.Equal("manager", result.View);
        repository.Verify(repo => repo.GetBranchesAsync(It.IsAny<DashboardScope>(), It.IsAny<DashboardPeriod>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OwnerResponseKeepsReceiptsAndRefundsSeparate()
    {
        var result = await Service(Repository().Object, RoleCode.Owner).GetAsync(RoleCode.Owner, new(), default);
        Assert.Equal(90, result.Financials!.CollectedAmount);
        Assert.Equal(5, result.Financials.RefundedAmount);
        Assert.Equal(85, result.Financials.NetCollectedAmount);
        Assert.Equal(10, result.Financials.DepositCollectedAmount);
        Assert.Equal(80, result.Financials.SessionCollectedAmount);
        Assert.Equal("real", result.PaymentMode);
    }

    [Fact]
    public async Task ResponseRefactoringPreservesPublicJsonPropertyContract()
    {
        var result = await Service(Repository().Object, RoleCode.Owner).GetAsync(RoleCode.Owner, new(), default);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(new[]
        {
            "view", "generatedAtUtc", "timeZone", "from", "to", "paymentMode", "restaurants", "hasMoreRestaurants",
            "metrics", "financials", "chart", "alerts", "branchRows", "sessionRows", "pageIndex", "pageSize", "totalRows"
        }, json.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "key", "value", "unit", "previousValue", "changePercent" },
            json.GetProperty("metrics")[0].EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData(0, "real", 201)]
    [InlineData(-1, "real", 201)]
    [InlineData(100001, "real", 201)]
    [InlineData(1, "both", 201)]
    [InlineData(1, "real", 0)]
    public async Task InvalidFiltersNeverReachDataQueries(int page, string paymentMode, int restaurantId)
    {
        var repository = new Mock<IDashboardRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<BadRequestException>(() => Service(repository.Object, RoleCode.Owner)
            .GetAsync(RoleCode.Owner, new() { PageIndex = page, PaymentMode = paymentMode, RestaurantId = restaurantId }, default));
        Assert.Empty(repository.Invocations);
    }

    [Fact]
    public async Task MissingManagerRestaurantAndUnauthorizedPlatformAccessAreRejected()
    {
        var repository = new Mock<IDashboardRepository>(MockBehavior.Strict);
        var service = Service(repository.Object, RoleCode.Owner);
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetAsync(RoleCode.Manager, new(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAsync(RoleCode.SuperAdmin, new(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAsync(RoleCode.Waiter, new(), default));
        Assert.Empty(repository.Invocations);
    }

    [Fact]
    public void DashboardControllerDisablesBrowserCaching()
    {
        var cache = Assert.Single(typeof(DashboardController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }

    /// <summary>Test üçün layihənin real query filter-ləri olan ayrıca yaddaş konteksti yaradır.</summary>
    private static ECafeDbContext Context() => new(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Rol üzrə mapper sınaqlarında müsbət saylı canlı vəziyyət nümunəsi verir.</summary>
    private static DashboardLive Live() => new(2, 5, 2, 24, 8, 11, 4, 2, 2, 3, 1, 2, 1, 1, 1);

    /// <summary>Xidmət qatında ardıcıllığı və məxfilik sərhədlərini sınamaq üçün sabit oxuma nəticələri qurur.</summary>
    private static Mock<IDashboardRepository> Repository()
    {
        var repository = new Mock<IDashboardRepository>();
        repository.Setup(repo => repo.ResolveAccessAsync(It.IsAny<DashboardScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardAccess([], false, "Asia/Baku"));
        repository.Setup(repo => repo.GetTotalsAsync(It.IsAny<DashboardScope>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardTotals(10, 100, 20, 90, 10, 80, 5));
        repository.Setup(repo => repo.GetLiveAsync(It.IsAny<DashboardScope>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Live());
        repository.Setup(repo => repo.GetChartAsync(It.IsAny<DashboardScope>(), It.IsAny<DashboardPeriod>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DashboardHour>());
        repository.Setup(repo => repo.GetBranchesAsync(It.IsAny<DashboardScope>(), It.IsAny<DashboardPeriod>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DashboardPage<DashboardBranchRow>([], 0));
        repository.Setup(repo => repo.GetSessionsAsync(It.IsAny<DashboardScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DashboardPage<TableSession>([], 0));
        return repository;
    }

    /// <summary>HTTP rolunu və vaxtı saxtalaşdıraraq yalnız dashboard xidmətinin davranışını yoxlayır.</summary>
    private static DashboardManager Service(IDashboardRepository repository, RoleCode role)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", "101"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test")) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == context);
        var clock = new Mock<TimeProvider>(); clock.Setup(value => value.GetUtcNow()).Returns(Now);
        return new(accessor, Mock.Of<IMapper>(), new ConfigurationBuilder().Build(), repository, clock.Object);
    }
}
