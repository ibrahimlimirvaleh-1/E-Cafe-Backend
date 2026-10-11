using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationCheckInWindowTests
{
    [Theory]
    [InlineData(-10, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public async Task MarkArrivalWindow_EndsAtNoShowDeadline(int minutesFromReservation, bool expected)
    {
        var reservedAt = new DateTime(2026, 10, 4, 11, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        AddOpenRestaurant(context);
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            PeopleCount = 2, ReservedAt = reservedAt,
            NoShowDeadlineAt = reservedAt.AddMinutes(15)
        });
        await context.SaveChangesAsync();

        var repository = new ReservationRepository(context);
        Assert.Equal(expected, await repository.IsMarkArrivalWindowOpenAsync(
            2, 1, reservedAt.AddMinutes(minutesFromReservation)));

        var reservation = await context.Reservations.SingleAsync();
        reservation.ArrivedAt = reservedAt.AddMinutes(10);
        await context.SaveChangesAsync();
        Assert.False(await repository.IsMarkArrivalWindowOpenAsync(2, 1, reservedAt.AddMinutes(14)));
        Assert.True(await repository.IsCheckInWindowOpenAsync(2, 1, reservedAt.AddMinutes(20)));

        reservation.MustVacateAt = reservedAt.AddMinutes(25);
        await context.SaveChangesAsync();
        Assert.False(await repository.IsCheckInWindowOpenAsync(2, 1, reservedAt.AddMinutes(25)));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public async Task CheckInWindow_EndsAtNoShowDeadline(
        int minutesFromReservation,
        bool expected)
    {
        var reservedAt = new DateTime(2026, 10, 4, 11, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var context = new ECafeDbContext(options);
        context.Restaurants.Add(new Restaurant
        {
            Id = 2, Name = "Test", Location = "Baku", Phone = "1",
            IsActive = true, TimeZone = "Asia/Baku"
        });
        context.RestaurantWorkingHours.Add(new RestaurantWorkingHour
        {
            Id = 1, RestaurantId = 2, DayOfWeek = DayOfWeek.Sunday,
            OpensAt = new TimeOnly(9, 0), ClosesAt = new TimeOnly(23, 0)
        });
        context.Tables.Add(new Table { Id = 3, RestaurantId = 2, TableNo = 1 });
        context.Users.Add(new User
        {
            Id = 4, Name = "Test", Surname = "User", Email = "test@example.com",
            Phone = "2", Password = "test"
        });
        context.Statuses.Add(new Status
        {
            Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed"
        });
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            PeopleCount = 2, ReservedAt = reservedAt,
            NoShowDeadlineAt = reservedAt.AddMinutes(15)
        });
        await context.SaveChangesAsync();

        var repository = new ReservationRepository(context);
        var result = await repository.IsCheckInWindowOpenAsync(
            2, 1, reservedAt.AddMinutes(minutesFromReservation));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-24 * 60, false)]
    [InlineData(-7 * 60, false)]
    [InlineData(-10, true)]
    public async Task EarlyArrivalMustBelongToBookingServiceInterval(int arrivalOffsetMinutes, bool expected)
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        AddOpenRestaurant(context);
        var reservedAt = new DateTime(2026, 10, 4, 11, 0, 0, DateTimeKind.Utc);
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), ReservedAt = reservedAt,
            NoShowDeadlineAt = reservedAt.AddMinutes(15)
        });
        await context.SaveChangesAsync();
        Assert.Equal(expected, await new ReservationRepository(context).IsMarkArrivalWindowOpenAsync(
            2, 1, reservedAt.AddMinutes(arrivalOffsetMinutes)));
    }

    /// <summary>Rezervasiyanın yerli tarixində açıq restoran və iş intervalını test bazasına əlavə edir.</summary>
    private static void AddOpenRestaurant(ECafeDbContext context)
    {
        context.Restaurants.Add(new Restaurant
        {
            Id = 2, Name = "Test", Location = "Baku", Phone = "1",
            IsActive = true, TimeZone = "Asia/Baku"
        });
        context.RestaurantWorkingHours.Add(new RestaurantWorkingHour
        {
            Id = 1, RestaurantId = 2, DayOfWeek = DayOfWeek.Sunday,
            OpensAt = new TimeOnly(9, 0), ClosesAt = new TimeOnly(23, 0)
        });
    }
}
