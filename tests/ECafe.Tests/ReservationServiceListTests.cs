using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationServiceListTests
{
    [Fact]
    public async Task ServiceListOnlyContainsActiveReservationsForRequestedTable()
    {
        var reservedAt = new DateTime(2026, 10, 7, 16, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        context.Restaurants.Add(new Restaurant { Id = 1, Name = "Test", Location = "Baku", Phone = "1" });
        context.Tables.AddRange(
            new Table { Id = 1, RestaurantId = 1, TableNo = 1 },
            new Table { Id = 2, RestaurantId = 1, TableNo = 2 });
        context.Users.Add(new User
        {
            Id = 1, Name = "Test", Surname = "Guest", Email = "guest@example.com",
            Phone = "2", Password = "test"
        });
        foreach (var (id, tableId, status) in new[]
        {
            (1, 1, ReservationStatus.Confirmed),
            (2, 1, ReservationStatus.Seated),
            (3, 1, ReservationStatus.Cancelled),
            (4, 2, ReservationStatus.Confirmed)
        })
        {
            context.Reservations.Add(new Reservation
            {
                Id = id, RestaurantId = 1, TableId = tableId, CustomerUserId = 1,
                StatusId = StatusIds.Reservation(status), PeopleCount = 2,
                ReservedAt = reservedAt, NoShowDeadlineAt = reservedAt.AddMinutes(15)
            });
        }
        await context.SaveChangesAsync();

        var page = await new ReservationRepository(context).GetForServiceAsync(
            1, new RestaurantReservationsQueryRequest { TableId = 1 });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 2, 1 }, page.Items.Select(reservation => reservation.Id).ToArray());
        Assert.All(page.Items, reservation => Assert.True(
            reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) ||
            reservation.StatusId == StatusIds.Reservation(ReservationStatus.Seated)));
    }

    [Fact]
    public async Task ServiceDateFilterUsesRestaurantLocalDay()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", TimeZone = "Asia/Baku"
        });
        context.Tables.Add(new Table { Id = 1, RestaurantId = 1, TableNo = 1 });
        context.Users.Add(new User
        {
            Id = 1, Name = "Test", Surname = "Guest", Email = "guest@example.com",
            Phone = "2", Password = "test"
        });
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), PeopleCount = 2,
            ReservedAt = new DateTime(2026, 10, 6, 21, 30, 0, DateTimeKind.Utc),
            NoShowDeadlineAt = new DateTime(2026, 10, 6, 21, 45, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var repository = new ReservationRepository(context);
        var octoberSeventh = await repository.GetForServiceAsync(1,
            new RestaurantReservationsQueryRequest
            {
                ReservedDate = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero)
            });
        var octoberSixth = await repository.GetForServiceAsync(1,
            new RestaurantReservationsQueryRequest
            {
                ReservedDate = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)
            });

        Assert.Single(octoberSeventh.Items);
        Assert.Empty(octoberSixth.Items);
    }

    [Fact]
    public async Task RestaurantDateFiltersUseLocalDayWhenUtcDateIsPreviousDay()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1", TimeZone = "Asia/Baku"
        });
        context.Tables.Add(new Table { Id = 1, RestaurantId = 1, TableNo = 1 });
        context.Users.Add(new User
        {
            Id = 1, Name = "Test", Surname = "Guest", Email = "guest@example.com",
            Phone = "2", Password = "test"
        });

        context.Statuses.Add(new Status
        {
            Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed"
        });

        var utcTimes = new[]
        {
            new DateTime(2026, 10, 7, 19, 59, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 8, 19, 59, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc)
        };
        for (var index = 0; index < utcTimes.Length; index++)
        {
            context.Reservations.Add(new Reservation
            {
                Id = index + 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
                StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), PeopleCount = 2,
                ReservedAt = utcTimes[index], NoShowDeadlineAt = utcTimes[index].AddMinutes(15)
            });
        }
        await context.SaveChangesAsync();

        var repository = new ReservationRepository(context);
        var filters = new RestaurantReservationsQueryRequest
        {
            ReservedDate = new DateTimeOffset(2026, 10, 7, 20, 0, 0, TimeSpan.Zero)
        };
        var (startUtc, endUtc) = RestaurantTimeZoneConverter.GetUtcDayRange(filters.ReservedDate.Value, "Asia/Baku");
        Assert.Equal(utcTimes[1], startUtc);
        Assert.Equal(utcTimes[3], endUtc);

        var servicePage = await repository.GetForServiceAsync(1, filters);
        var restaurantPage = await repository.GetForRestaurantAsync(1, filters);

        Assert.Equal(new[] { 3, 2 }, servicePage.Items.Select(reservation => reservation.Id).ToArray());
        Assert.Equal(new[] { 3, 2 }, restaurantPage.Items.Select(reservation => reservation.Id).ToArray());
    }

    [Fact]
    public async Task RestaurantOpeningCheckUsesLocalTime()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        context.Restaurants.Add(new Restaurant
        {
            Id = 1, Name = "Test", Location = "Baku", Phone = "1",
            TimeZone = "Asia/Baku", IsActive = true,
            WorkingHours = [new RestaurantWorkingHour
            {
                DayOfWeek = DayOfWeek.Wednesday,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(21, 0)
            }]
        });
        await context.SaveChangesAsync();

        var repository = new ReservationRepository(context);
        Assert.True(await repository.IsRestaurantOpenAsync(
            1, new DateTime(2026, 10, 7, 16, 0, 0, DateTimeKind.Utc)));
        Assert.False(await repository.IsRestaurantOpenAsync(
            1, new DateTime(2026, 10, 7, 17, 0, 0, DateTimeKind.Utc)));
    }
}
