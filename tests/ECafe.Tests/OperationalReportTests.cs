using System.Text;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Mappings;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class OperationalReportTests
{
    [Theory]
    [InlineData("products")]
    [InlineData("kitchen")]
    [InlineData("cancellations")]
    [InlineData("reservation-outcomes")]
    [InlineData("tables")]
    [InlineData("settlements")]
    [InlineData("branches")]
    [InlineData("stock")]
    public void NewReportsValidateTheirOwnFilters(string kind)
    {
        DashboardReportPolicy.Resolve(RoleCode.Owner, kind, null, "all");
        Assert.Throws<BadRequestException>(() => DashboardReportPolicy.Resolve(RoleCode.Owner, kind, 2001, "all"));
        Assert.Throws<BadRequestException>(() => DashboardReportPolicy.Resolve(RoleCode.Owner, kind, null, "receipt"));
    }

    [Theory]
    [InlineData("settlements")]
    [InlineData("branches")]
    public void ManagerCannotAccessFinancialExtensions(string kind)
        => Assert.Throws<ForbiddenException>(() => DashboardReportPolicy.Resolve(RoleCode.Manager, kind, null, "all"));

    [Fact]
    public void ReportDayStartsAtChosenLocalHourAndEarlyMorningBelongsToPreviousDay()
    {
        var now = new DateTimeOffset(2026, 11, 2, 22, 30, 0, TimeSpan.Zero);
        var period = DashboardReportPeriodPolicy.Resolve("today", null, null, "Asia/Baku", now, 4);
        Assert.Equal(new DateOnly(2026, 11, 2), period.From);
        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), period.StartUtc);
        Assert.Equal(period.StartUtc.AddDays(1), period.EndUtc);
        Assert.Throws<BadRequestException>(() => DashboardReportPeriodPolicy.Resolve("today", null, null, "Asia/Baku", now, 13));
        Assert.Throws<BadRequestException>(() => DashboardReportPeriodPolicy.Resolve("today", null, null, "Asia/Baku", now, -1));
    }

    [Fact]
    public void InvalidDaylightSavingBoundaryIsRejectedInsteadOfChangingBusinessWindow()
        => Assert.Throws<BadRequestException>(() => DashboardReportPeriodPolicy.Resolve("custom", new(2026, 3, 29), new(2026, 3, 29),
            "Europe/Berlin", new(2026, 3, 29, 12, 0, 0, TimeSpan.Zero), 2));

    [Fact]
    public void TypedDetailCsvKeepsNullsUnitsAndInjectionProtection()
    {
        var data = DashboardReportDetailMapper.Map(1, "ingredient unit consumed:number", [DashboardReportDetailMapper.Row("1", "=FORMULA", "kg", (decimal?)null)]);
        var response = new DashboardReportResponse("stock", DateTime.UtcNow, "Asia/Baku", new(2026, 11, 1), new(2026, 11, 2), "test", [], [], [], [], 1, 20, 1, data.Detail, 4);
        var csv = Encoding.UTF8.GetString(DashboardReportCsvMapper.Map(response).Content);
        Assert.Contains("'\u003dFORMULA", csv);
        Assert.Contains("\"kg\",\"\"", csv);
        Assert.Contains("not_applicable", csv);
        Assert.Contains("day_start_hour", csv);
        Assert.DoesNotContain("-test", DashboardReportCsvMapper.Map(response).FileName);
    }
}
