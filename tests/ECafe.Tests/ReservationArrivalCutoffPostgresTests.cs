using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Migrations;
using ECafe.Infrastructure.Repositories.Restaurant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    [StockPostgresFact]
    public async Task ArrivalCutoffMigrationPreservesOldWorkingHoursAndReservationTerms()
    {
        await using var f = await Database.CreateAsync(1);
        await using var db = f.Context();
        var reservedAt = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(19), DateTimeKind.Utc);
        var hour = new RestaurantWorkingHour { Id = 1001, RestaurantId = 1001,
            DayOfWeek = reservedAt.DayOfWeek, OpensAt = new(9, 0), ClosesAt = TimeOnly.MinValue, CloseDayOffset = 1 };
        db.Set<RestaurantWorkingHour>().Add(hour);
        db.Reservations.Add(new Reservation { Id = 1001, RestaurantId = 1001, TableId = 1001, CustomerUserId = 1001,
            PeopleCount = 2, ReservedAt = reservedAt, NoShowDeadlineAt = reservedAt.AddMinutes(15),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), DepositAmount = 17 });
        await db.SaveChangesAsync();
        // Yalnız bu fixture-in yaratdığı GUID bazasında köhnə sxemi təkrar qurur.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE core.restaurant_working_hours DROP CONSTRAINT ck_restaurant_working_hours_arrival_cutoff;
            ALTER TABLE core.restaurant_working_hours DROP COLUMN last_reservation_arrival_at;
            ALTER TABLE core.restaurant_working_hours DROP COLUMN last_reservation_arrival_day_offset;
            ALTER TABLE ops.reservations DROP CONSTRAINT ck_reservations_arrival_cutoff;
            ALTER TABLE ops.reservations DROP COLUMN reservation_arrival_cutoff_at;
            """);
        var migration = new AddRestaurantReservationArrivalCutoff();
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        db.ChangeTracker.Clear();
        var legacy = await db.Reservations.SingleAsync();
        Assert.Equal(reservedAt, legacy.ReservedAt);
        Assert.Equal(17, legacy.DepositAmount);
        Assert.Equal(reservedAt.AddMinutes(15), legacy.NoShowDeadlineAt);
        Assert.Null(legacy.ReservationArrivalCutoffAt);
        var saved = await db.Set<RestaurantWorkingHour>().SingleAsync();
        Assert.Null(saved.LastReservationArrivalAt);
        Assert.Null(saved.LastReservationArrivalDayOffset);
        saved.LastReservationArrivalAt = new(23, 0);
        saved.LastReservationArrivalDayOffset = 0;
        await db.SaveChangesAsync();
        Assert.Null((await db.Reservations.AsNoTracking().SingleAsync()).ReservationArrivalCutoffAt);
    }

    [StockPostgresFact]
    public async Task ArrivalCutoffDatabaseConstraintsRejectInvalidClockOffsetsAndRetroactiveSnapshots()
    {
        await using var f = await Database.CreateAsync(1);
        await using var db = f.Context();
        db.Set<RestaurantWorkingHour>().Add(new RestaurantWorkingHour { Id = 1001, RestaurantId = 1001,
            DayOfWeek = DayOfWeek.Thursday, OpensAt = new(9, 0), ClosesAt = new(2, 0), CloseDayOffset = 1,
            LastReservationArrivalAt = new(1, 0), LastReservationArrivalDayOffset = 1 });
        await db.SaveChangesAsync();
        foreach (var statement in new[]
        {
            "UPDATE core.restaurant_working_hours SET last_reservation_arrival_day_offset = 2 WHERE id = 1001",
            "UPDATE core.restaurant_working_hours SET last_reservation_arrival_at = '08:00', last_reservation_arrival_day_offset = 0 WHERE id = 1001",
            "UPDATE core.restaurant_working_hours SET last_reservation_arrival_at = '02:00', last_reservation_arrival_day_offset = 1 WHERE id = 1001",
            "UPDATE core.restaurant_working_hours SET last_reservation_arrival_at = NULL WHERE id = 1001"
        })
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(statement))).SqlState);
        var reservedAt = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(19), DateTimeKind.Utc);
        db.Reservations.Add(new Reservation { Id = 1001, RestaurantId = 1001, TableId = 1001, CustomerUserId = 1001,
            PeopleCount = 2, ReservedAt = reservedAt, ReservationArrivalCutoffAt = reservedAt,
            NoShowDeadlineAt = reservedAt.AddMinutes(15), StatusId = StatusIds.Reservation(ReservationStatus.Confirmed) });
        await db.SaveChangesAsync();
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE ops.reservations SET reservation_arrival_cutoff_at = reserved_at - INTERVAL '1 minute' WHERE id = 1001"))).SqlState);
    }

    [StockPostgresFact]
    public async Task ArrivalCutoffUpdateIsVisibleToTheNextReservationAfterTheSharedScheduleLock()
    {
        await using var f = await Database.CreateAsync(1);
        var reservedAt = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(19), DateTimeKind.Utc);
        await using (var seed = f.Context())
        {
            seed.Set<RestaurantWorkingHour>().Add(new RestaurantWorkingHour { RestaurantId = 1001,
                DayOfWeek = reservedAt.DayOfWeek, OpensAt = new(9, 0), ClosesAt = TimeOnly.MinValue, CloseDayOffset = 1,
                LastReservationArrivalAt = new(23, 0), LastReservationArrivalDayOffset = 0 });
            await seed.SaveChangesAsync();
        }
        await using var a = f.Context();
        await using var b = f.Context();
        await using var change = await a.Database.BeginTransactionAsync();
        await new RestaurantRepository(a).AcquireScheduleLockAsync(1001);
        (await a.Set<RestaurantWorkingHour>().SingleAsync()).LastReservationArrivalAt = new(22, 0);
        await a.SaveChangesAsync();
        await using var booking = await b.Database.BeginTransactionAsync();
        var waiting = new RestaurantRepository(b).AcquireScheduleLockAsync(1001);
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);
        await change.CommitAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        var cutoff = await new RestaurantRepository(b).GetReservationArrivalCutoffAsync(1001, new(reservedAt));
        Assert.Equal(reservedAt.AddHours(-1), cutoff);
        Assert.False(RestaurantReservationArrivalPolicy.Allows(reservedAt, cutoff));
        await booking.RollbackAsync();
    }
}
