using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantReservationTableFilterTests
{
    [Fact]
    public async Task TableAndDateFiltersAreAppliedBeforePagination()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var day = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        await using (var context = new ECafeDbContext(options))
        {
            context.Restaurants.AddRange(
                new Restaurant { Id = 2, Name = "A", Location = "Baku", Phone = "1" },
                new Restaurant { Id = 6, Name = "B", Location = "Baku", Phone = "2" });
            context.Tables.AddRange(
                new Table { Id = 3, RestaurantId = 2, TableNo = 1 },
                new Table { Id = 4, RestaurantId = 2, TableNo = 2 },
                new Table { Id = 5, RestaurantId = 6, TableNo = 1 });
            context.Users.Add(new User { Id = 7, Name = "Test", Surname = "User", Email = "test@example.com", Phone = "3", Password = "test" });
            context.Statuses.Add(new Status { Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed" });
            context.Reservations.AddRange(
                CreateReservation(1, 2, 3, day.AddHours(10)),
                CreateReservation(2, 2, 3, day.AddHours(12)),
                CreateReservation(3, 2, 4, day.AddHours(13)),
                CreateReservation(4, 2, 3, day.AddDays(1).AddHours(10)),
                CreateReservation(5, 6, 5, day.AddHours(14)));
            await context.SaveChangesAsync();
        }

        await using var reader = new ECafeDbContext(options);
        var repository = new ReservationRepository(reader);
        var page = await repository.GetForRestaurantAsync(2, new RestaurantReservationsQueryRequest
        {
            TableId = 3,
            ReservedDate = new DateTimeOffset(day),
            PageNumber = 2,
            PageSize = 1
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.PageIndex);
        Assert.Equal(1, Assert.Single(page.Items).Id);
    }

    private static Reservation CreateReservation(int id, int restaurantId, int tableId, DateTime reservedAt)
        => new()
        {
            Id = id, RestaurantId = restaurantId, TableId = tableId, CustomerUserId = 7,
            PeopleCount = 2, StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            ReservedAt = reservedAt
        };
}
