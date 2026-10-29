using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class RestaurantScheduleTests
{
    [Theory]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    public async Task CutoffOnlyChangeCanBeAppliedByActiveManagerOrOwnerWithoutRewritingExistingReservations(RoleCode role)
    {
        await using var f = new Fixture();
        f.Assignments.Setup(repository => repository.GetActiveRoleIdAsync(7, 2)).ReturnsAsync((int)role);
        f.Reservation.ReservationArrivalCutoffAt = Arrival.AddHours(4);
        var original = (f.Reservation.ReservedAt, f.Reservation.NoShowDeadlineAt, f.Reservation.ReservationArrivalCutoffAt,
            f.Reservation.DepositAmount, f.Reservation.CancellationDeadline);
        var request = f.Request(0);
        foreach (var hour in request.WorkingHours)
        {
            hour.CloseDayOffset = 1;
            hour.LastReservationArrivalAt = new TimeOnly(18, 0);
            hour.LastReservationArrivalDayOffset = 0;
        }
        var proposal = await f.Service.ProposeAsync(2, request, default);
        Assert.Empty(proposal.Participants);
        Assert.True(proposal.CanApply);
        Assert.All(f.Restaurant.WorkingHours, hour => Assert.Null(hour.LastReservationArrivalAt));
        var applied = await f.Service.ApplyAsync(2, new(proposal.Token), default);
        Assert.Equal("Applied", applied.State);
        Assert.All(f.Restaurant.WorkingHours, hour => Assert.Equal(new TimeOnly(18, 0), hour.LastReservationArrivalAt));
        Assert.Equal(original, (f.Reservation.ReservedAt, f.Reservation.NoShowDeadlineAt, f.Reservation.ReservationArrivalCutoffAt,
            f.Reservation.DepositAmount, f.Reservation.CancellationDeadline));
        Assert.Empty(f.Reservation.StatusHistory);
        Assert.Null(f.Reservation.CancelledAt);
        Assert.Null(f.Reservation.MustVacateAt);
    }

    [Theory]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    [InlineData(RoleCode.Customer)]
    public async Task OtherLiveRolesCannotProposeCutoffEvenWithManagerClaims(RoleCode role)
    {
        await using var f = new Fixture();
        f.Assignments.Setup(repository => repository.GetActiveRoleIdAsync(7, 2)).ReturnsAsync((int)role);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.ProposeAsync(2, f.Request(), default));
        Assert.Empty(await f.Db.RestaurantScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task RevokedManagerRoleIsCheckedAgainAfterAcquiringScheduleLock()
    {
        await using var f = new Fixture();
        f.Assignments.SetupSequence(repository => repository.GetActiveRoleIdAsync(7, 2))
            .ReturnsAsync((int)RoleCode.Manager).ReturnsAsync((int)RoleCode.Waiter);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.ProposeAsync(2, f.Request(), default));
        Assert.Empty(await f.Db.RestaurantScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task DirectCutoffChangeRequiresLiveManagerOrOwnerAndDoesNotNeedCustomerConsent()
    {
        await using var f = new Fixture();
        var request = f.Request(0);
        foreach (var hour in request.WorkingHours)
        {
            hour.CloseDayOffset = 1;
            hour.LastReservationArrivalAt = new TimeOnly(18, 0);
            hour.LastReservationArrivalDayOffset = 0;
        }
        var normalized = ScheduleTerms.Normalize(request.WorkingHours);
        f.Assignments.Setup(repository => repository.GetActiveRoleIdAsync(7, 2)).ReturnsAsync((int)RoleCode.Waiter);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.EnsureDirectChangeAllowedAsync(f.Restaurant, normalized, "Asia/Baku", default));
        f.Assignments.Setup(repository => repository.GetActiveRoleIdAsync(7, 2)).ReturnsAsync((int)RoleCode.Owner);
        await f.Service.EnsureDirectChangeAllowedAsync(f.Restaurant, normalized, "Asia/Baku", default);
        Assert.Null(f.Reservation.CancelledAt);
    }
}
