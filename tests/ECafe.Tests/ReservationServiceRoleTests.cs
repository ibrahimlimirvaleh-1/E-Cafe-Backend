using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationServiceRoleTests
{
    [Fact]
    public void SeededActionsSeparateArrivalFromSeating()
    {
        using var context = CreateContext();
        var rules = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkflowActionRule))!
            .GetSeedData().Where(row => (int)row[nameof(WorkflowActionRule.StatusId)]! ==
                StatusIds.Reservation(ReservationStatus.Confirmed)).ToList();

        Assert.Contains(rules, row =>
            (int)row[nameof(WorkflowActionRule.RoleId)]! == (int)RoleCode.Waiter &&
            (string)row[nameof(WorkflowActionRule.ActionCode)]! == WorkflowActionCode.Reservation.CheckIn);
        Assert.DoesNotContain(rules, row =>
            (int)row[nameof(WorkflowActionRule.RoleId)]! == (int)RoleCode.Manager &&
            (string)row[nameof(WorkflowActionRule.ActionCode)]! == WorkflowActionCode.Reservation.CheckIn);
        Assert.DoesNotContain(rules, row =>
            (int)row[nameof(WorkflowActionRule.RoleId)]! == (int)RoleCode.Owner &&
            (string)row[nameof(WorkflowActionRule.ActionCode)]! == WorkflowActionCode.Reservation.CheckIn);
        Assert.All(new[] { RoleCode.Manager, RoleCode.Owner, RoleCode.Waiter }, role =>
            Assert.Contains(rules, row =>
                (int)row[nameof(WorkflowActionRule.RoleId)]! == (int)role &&
                (string)row[nameof(WorkflowActionRule.ActionCode)]! == WorkflowActionCode.Reservation.MarkArrived));
    }

    [Fact]
    public void SeededPermissionsDoNotAllowManagerOrPlatformAdminToSeat()
    {
        using var context = CreateContext();
        var permissions = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(RolePermission))!.GetSeedData().ToList();

        Assert.Contains(permissions, row =>
            (int)row[nameof(RolePermission.RoleId)]! == (int)RoleCode.Waiter &&
            (int)row[nameof(RolePermission.PermissionId)]! == (int)PermissionCode.SeatReservationGuest);
        Assert.DoesNotContain(permissions, row =>
            (int)row[nameof(RolePermission.RoleId)]! != (int)RoleCode.Waiter &&
            (int)row[nameof(RolePermission.PermissionId)]! == (int)PermissionCode.SeatReservationGuest);
    }

    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
