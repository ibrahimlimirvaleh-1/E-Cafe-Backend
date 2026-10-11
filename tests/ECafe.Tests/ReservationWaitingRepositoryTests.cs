using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationWaitingRepositoryTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WaitingQueueFiltersRestaurantStateAndOrdersOldestFirstWithPagination()
    {
        await using var context = CreateContext();
        context.Reservations.AddRange(
            CreateReservation(1, arrivedMinutesAgo: 5),
            CreateReservation(2, arrivedMinutesAgo: 20),
            CreateReservation(3, arrivedMinutesAgo: 10),
            CreateReservation(4, arrivedMinutesAgo: null),
            CreateReservation(5, arrivedMinutesAgo: 30, status: ReservationStatus.Seated),
            CreateReservation(6, arrivedMinutesAgo: 30, restaurantId: 2));
        await context.SaveChangesAsync();
        var repository = new ReservationRepository(context);
        var filter = new RestaurantReservationsQueryRequest { WaitingOnly = true, PageSize = 2 };
        var page = await repository.GetForServiceAsync(1, filter);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { 2, 3 }, page.Items.Select(r => r.Id));
        filter.PageNumber = 2;
        var second = await repository.GetForRestaurantAsync(1, filter);
        Assert.Equal(new[] { 1 }, second.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task WaitingCandidatesExcludeEarlyArrivalsAlreadyNotifiedAndClosedGuests()
    {
        await using var context = CreateContext();
        var notified = CreateReservation(3, 20);
        notified.WaitingEscalatedAt = Now.AddMinutes(-1);
        var early = CreateReservation(4, 30);
        early.ReservedAt = Now.AddMinutes(5);
        context.Reservations.AddRange(
            CreateReservation(1, 15), CreateReservation(2, 14), notified, early,
            CreateReservation(5, 30, status: ReservationStatus.Cancelled));
        await context.SaveChangesAsync();
        var repository = new ReservationRepository(context);
        var candidates = await repository.GetWaitingEscalationCandidatesAsync(Now, 15, 100);
        Assert.Equal(new[] { 1 }, candidates.Select(c => c.ReservationId));
        Assert.Empty(await repository.GetNoShowCandidatesAsync(Now.AddHours(1), 100));
    }

    [Fact]
    public async Task EarlyArrivalAcrossMidnightUsesSameRestaurantWorkInterval()
    {
        await using var context = CreateContext();
        context.RestaurantWorkingHours.Add(new RestaurantWorkingHour
        {
            RestaurantId = 1, DayOfWeek = DayOfWeek.Sunday,
            OpensAt = new TimeOnly(18, 0), ClosesAt = new TimeOnly(2, 0), CloseDayOffset = 1
        });
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 1, TableId = 1, CustomerUserId = 1,
            ReservedAt = new DateTime(2026, 10, 11, 20, 10, 0, DateTimeKind.Utc),
            NoShowDeadlineAt = new DateTime(2026, 10, 11, 20, 25, 0, DateTimeKind.Utc),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed)
        });
        await context.SaveChangesAsync();
        Assert.True(await new ReservationRepository(context).IsMarkArrivalWindowOpenAsync(
            1, 1, new DateTime(2026, 10, 11, 19, 50, 0, DateTimeKind.Utc)));
    }

    /// <summary>İki restoranın məlumatlarını ayırmaq və repository filtrlərini yoxlamaq üçün təcrid edilmiş test bazası yaradır.</summary>
    private static ECafeDbContext CreateContext()
    {
        var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Restaurants.AddRange(
            new Restaurant { Id = 1, Name = "Test", Phone = "1", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true },
            new Restaurant { Id = 2, Name = "Other", Phone = "2", Location = "Baku", TimeZone = "Asia/Baku", IsActive = true });
        context.Tables.Add(new Table { Id = 1, RestaurantId = 1 });
        context.Users.Add(new User { Id = 1, Name = "Guest", Surname = "Test", Phone = "1", Email = "guest@example.com", Password = "test" });
        foreach (var status in new[] { ReservationStatus.Confirmed, ReservationStatus.Seated, ReservationStatus.Cancelled })
            context.Statuses.Add(new Status { Id = StatusIds.Reservation(status), Name = status.ToString() });
        return context;
    }

    /// <summary>Gəliş və status sərhədləri məlum olan test rezervasiyası hazırlayır.</summary>
    private static Reservation CreateReservation(int id, int? arrivedMinutesAgo,
        ReservationStatus status = ReservationStatus.Confirmed, int restaurantId = 1)
        => new()
        {
            Id = id, RestaurantId = restaurantId, TableId = 1, CustomerUserId = 1,
            StatusId = StatusIds.Reservation(status), ReservedAt = Now.AddMinutes(-30),
            ArrivedAt = arrivedMinutesAgo.HasValue ? Now.AddMinutes(-arrivedMinutesAgo.Value) : null,
            SeatedAt = status == ReservationStatus.Seated ? Now : null,
            NoShowDeadlineAt = Now.AddMinutes(15)
        };
}
