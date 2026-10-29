using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Repositories.Table;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed partial class ReservationSessionAvailabilityTests
{
    [Fact]
    public async Task NewAvailabilityIncludesExactLastArrivalAndRejectsLaterArrivalInListAndSingle()
    {
        await using var db = CreateContext();
        var cutoff = DateTime.UtcNow.Date.AddDays(2).AddHours(19);
        var table = await SeedAsync(db, cutoff.AddDays(-1), new(9, 0), TimeOnly.MinValue, 1);
        foreach (var hour in table.Restaurant.WorkingHours)
        {
            hour.LastReservationArrivalAt = new TimeOnly(23, 0);
            hour.LastReservationArrivalDayOffset = 0;
        }
        await db.SaveChangesAsync();
        var repository = new TableRepository(db);
        Assert.Single(await repository.GetReservationTableAvailabilityAsync(2, new(cutoff)));
        Assert.NotNull(await repository.GetReservationTableAvailabilityAsync(2, 3, new(cutoff)));
        Assert.Empty(await repository.GetReservationTableAvailabilityAsync(2, new(cutoff.AddMinutes(1))));
        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(cutoff.AddMinutes(1))));
    }

    [Fact]
    public async Task EarlierCurrentCutoffDoesNotBlockPreviouslyBookedReservationRecheckOrHideTheNextBooking()
    {
        await using var db = CreateContext();
        var target = DateTime.UtcNow.Date.AddDays(2).AddHours(19);
        var table = await SeedAsync(db, target.AddDays(-1), new(9, 0), TimeOnly.MinValue, 1);
        foreach (var hour in table.Restaurant.WorkingHours)
        {
            hour.LastReservationArrivalAt = new TimeOnly(22, 0);
            hour.LastReservationArrivalDayOffset = 0;
        }
        var status = new Status { Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed", BlocksTableAvailability = true };
        db.Reservations.Add(new Reservation { Id = 11, Restaurant = table.Restaurant, Table = table, CustomerUserId = 4,
            PeopleCount = 2, ReservedAt = target, ReservationArrivalCutoffAt = target.AddMinutes(30), Status = status });
        db.Reservations.Add(new Reservation { Id = 12, Restaurant = table.Restaurant, Table = table, CustomerUserId = 5,
            PeopleCount = 2, ReservedAt = target.AddMinutes(45), Status = status });
        await db.SaveChangesAsync();
        var repository = new TableRepository(db);
        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(target)));
        Assert.NotNull(await repository.GetReservationTableAvailabilityAsync(2, 3, new(target), 11));
        Assert.Equal(target.AddMinutes(45), await repository.GetNextReservationAtAsync(2, 3, 11, target));
    }

    [Fact]
    public async Task OvernightCutoffIsAppliedToPreviousWorkdayRatherThanTodaysEvening()
    {
        await using var db = CreateContext();
        var evening = DateTime.UtcNow.Date.AddDays(2).AddHours(19);
        var table = await SeedAsync(db, evening.AddDays(-1), new(19, 0), new(2, 0), 1);
        foreach (var hour in table.Restaurant.WorkingHours)
        {
            hour.LastReservationArrivalAt = new TimeOnly(1, 0);
            hour.LastReservationArrivalDayOffset = 1;
        }
        await db.SaveChangesAsync();
        var repository = new TableRepository(db);
        Assert.NotNull(await repository.GetReservationTableAvailabilityAsync(2, 3, new(evening.AddHours(2))));
        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(evening.AddHours(2).AddMinutes(1))));
    }
}
