using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantReservationOrderingTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RestaurantList_UsesCreatedTimeAndId_NotArrivalTime()
    {
        await using var db = await CreateContextAsync();
        var page = await new ReservationRepository(db).GetForRestaurantAsync(2, new());

        Assert.Equal(new[] { 40, 20, 30, 10 }, page.Items.Select(reservation => reservation.Id));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task NewestCreationOrdering_IsAppliedBeforePagination()
    {
        await using var db = await CreateContextAsync();
        var repository = new ReservationRepository(db);
        var first = await repository.GetForRestaurantAsync(2, new() { PageNumber = 1, PageSize = 2 });
        var second = await repository.GetForRestaurantAsync(2, new() { PageNumber = 2, PageSize = 2 });

        Assert.Equal(new[] { 40, 20 }, first.Items.Select(reservation => reservation.Id));
        Assert.Equal(new[] { 30, 10 }, second.Items.Select(reservation => reservation.Id));
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, second.TotalPages);
    }

    [Fact]
    public async Task FilteredRestaurantList_PreservesNewestCreationOrdering()
    {
        await using var db = await CreateContextAsync();
        var page = await new ReservationRepository(db).GetForRestaurantAsync(2, new()
        {
            ReservedDate = new DateTimeOffset(Day), TableId = 3,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), PageNumber = 1, PageSize = 2
        });

        Assert.Equal(new[] { 40, 20 }, page.Items.Select(reservation => reservation.Id));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task WaitingList_StillPrioritizesLongestWaitingCustomer()
    {
        await using var db = await CreateContextAsync();
        var page = await new ReservationRepository(db).GetForRestaurantAsync(2, new() { WaitingOnly = true });

        Assert.Equal(new[] { 10, 30, 20, 40 }, page.Items.Select(reservation => reservation.Id));
    }

    [Fact]
    public async Task ServiceList_KeepsExistingArrivalTimeOrdering()
    {
        await using var db = await CreateContextAsync();
        var page = await new ReservationRepository(db).GetForServiceAsync(2, new());

        Assert.Equal(new[] { 30, 10, 20, 40 }, page.Items.Select(reservation => reservation.Id));
    }

    /// <summary>
    /// Sabit yaradılma və gəliş tarixləri olan qeydləri sınaq bazasına əlavə edir.
    /// Audit tarixləri yalnız fixture daxilində saxlanılır; bool parametrli DbContext overload-u onları yenidən yazmır.
    /// </summary>
    private static async Task<ECafeDbContext> CreateContextAsync()
    {
        var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Restaurants.Add(new Restaurant { Id = 2, Name = "Test", Location = "Baku", Phone = "1", TimeZone = "UTC" });
        db.Tables.Add(new Table { Id = 3, RestaurantId = 2, TableNo = 1, Name = "Masa-1" });
        db.Users.Add(new User { Id = 7, Name = "Test", Surname = "Customer", Email = "test@example.com", Phone = "1", Password = "test" });
        db.Statuses.Add(new Status { Id = StatusIds.Reservation(ReservationStatus.Confirmed), Name = "Confirmed" });
        db.Reservations.AddRange(
            CreateReservation(10, createdMinute: 0, arrivalHour: 16, waitingMinute: 0),
            CreateReservation(20, createdMinute: 10, arrivalHour: 15, waitingMinute: 20),
            CreateReservation(30, createdMinute: 5, arrivalHour: 19, waitingMinute: 10),
            CreateReservation(40, createdMinute: 10, arrivalHour: 12, waitingMinute: 30));
        await db.SaveChangesAsync(true, CancellationToken.None);
        return db;
    }

    /// <summary>Yaradılma sırasının gəliş vaxtı və gözləmə sırasından fərqli olduğu rezervasiyanı qurur.</summary>
    private static Reservation CreateReservation(int id, int createdMinute, int arrivalHour, int waitingMinute)
        => new()
        {
            Id = id, RestaurantId = 2, TableId = 3, CustomerUserId = 7, PeopleCount = 2,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            CreatedAt = Day.AddHours(10).AddMinutes(createdMinute),
            ReservedAt = Day.AddHours(arrivalHour), ArrivedAt = Day.AddHours(12).AddMinutes(waitingMinute)
        };
}
