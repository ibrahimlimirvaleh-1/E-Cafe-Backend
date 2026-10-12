using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Table;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationSessionAvailabilityTests
{
    [Fact]
    public async Task PreviousServiceSession_DoesNotHideFutureTable_InListOrSingleLookup()
    {
        await using var db = CreateContext();
        var target = DateTime.UtcNow.Date.AddDays(2).AddHours(15);
        await SeedAsync(db, target.AddDays(-1), new(9, 0), TimeOnly.MinValue, 1);
        var repository = new TableRepository(db);

        Assert.Single(await repository.GetReservationTableAvailabilityAsync(2, new(target)));
        Assert.NotNull(await repository.GetReservationTableAvailabilityAsync(2, 3, new(target)));
        Assert.True(await repository.HasOpenTableSessionAsync(2, 3));
    }

    [Fact]
    public async Task SessionWithinSelectedServiceInterval_BlocksTable()
    {
        await using var db = CreateContext();
        var target = DateTime.UtcNow.Date.AddDays(2).AddHours(15);
        await SeedAsync(db, target.AddHours(-1), new(9, 0), TimeOnly.MinValue, 1);
        var repository = new TableRepository(db);

        Assert.Empty(await repository.GetReservationTableAvailabilityAsync(2, new(target)));
        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(target)));
    }

    [Fact]
    public async Task OvernightSession_BlocksAfterMidnight_ButNotNextEvening()
    {
        await using var db = CreateContext();
        var evening = DateTime.UtcNow.Date.AddDays(2).AddHours(19); // 23:00 in Baku.
        await SeedAsync(db, evening, new(19, 0), new(2, 0), 1);
        var repository = new TableRepository(db);

        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(evening.AddHours(2))));
        Assert.NotNull(await repository.GetReservationTableAvailabilityAsync(2, 3, new(evening.AddDays(1))));
    }

    [Fact]
    public async Task StaleOpenSession_StillBlocksCurrentServiceInterval()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        await SeedAsync(db, now.AddDays(-2), TimeOnly.MinValue, TimeOnly.MinValue, 1);
        var repository = new TableRepository(db);

        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(now)));
    }

    [Fact]
    public async Task FutureConfirmedReservation_StillBlocksDespitePreviousServiceSession()
    {
        await using var db = CreateContext();
        var target = DateTime.UtcNow.Date.AddDays(2).AddHours(15);
        var table = await SeedAsync(db, target.AddDays(-1), new(9, 0), TimeOnly.MinValue, 1);
        db.Reservations.Add(new Reservation
        {
            Table = table, Restaurant = table.Restaurant, CustomerUserId = 4, PeopleCount = 2,
            ReservedAt = target, StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            Status = new Status
            {
                Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed",
                BlocksTableAvailability = true
            }
        });
        await db.SaveChangesAsync();
        var repository = new TableRepository(db);

        Assert.Empty(await repository.GetReservationTableAvailabilityAsync(2, new(target)));
        Assert.Null(await repository.GetReservationTableAvailabilityAsync(2, 3, new(target)));
    }

    [Fact]
    public async Task ClosedSession_DoesNotBlockSameInterval()
    {
        await using var db = CreateContext();
        var target = DateTime.UtcNow.Date.AddDays(2).AddHours(15);
        await SeedAsync(db, target.AddHours(-1), new(9, 0), TimeOnly.MinValue, 1);
        var session = await db.TableSessions.SingleAsync();
        session.ClosedAt = target.AddMinutes(-30);
        session.StatusId = StatusIds.TableSession(TableSessionStatus.Closed);
        await db.SaveChangesAsync();

        Assert.NotNull(await new TableRepository(db).GetReservationTableAvailabilityAsync(2, 3, new(target)));
    }

    /// <summary>
    /// Hər sınaq üçün ayrıca yaddaş bazası yaradaraq iş intervalı yoxlamalarını bir-birindən təcrid edir.
    /// </summary>
    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>
    /// Bakı saat qurşağında restoran, aktiv masa və verilmiş anda açılan sessiyanı sınaq bazasına əlavə edir.
    /// </summary>
    private static async Task<Table> SeedAsync(ECafeDbContext db, DateTime openedAt,
        TimeOnly opensAt, TimeOnly closesAt, int closeDayOffset)
    {
        var restaurant = new Restaurant
        {
            Id = 2, Name = "Test", Location = "Baku", Phone = "0514300858", TimeZone = "Asia/Baku",
            IsActive = true,
            WorkingHours = Enum.GetValues<DayOfWeek>().Select(day => new RestaurantWorkingHour
            {
                DayOfWeek = day, OpensAt = opensAt, ClosesAt = closesAt, CloseDayOffset = closeDayOffset
            }).ToList()
        };
        var table = new Table { Id = 3, Restaurant = restaurant, Name = "Masa-1", TableNo = 1, Capacity = 2, IsActive = true };
        db.TableSessions.Add(new TableSession
        {
            Table = table, Restaurant = restaurant, OpenedAt = openedAt,
            StatusId = StatusIds.TableSession(TableSessionStatus.Open)
        });
        await db.SaveChangesAsync();
        return table;
    }
}
