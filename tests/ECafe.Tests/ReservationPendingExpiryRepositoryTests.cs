using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationPendingExpiryRepositoryTests
{
    private static DbContextOptions<ECafeDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static async Task SeedAsync(DbContextOptions<ECafeDbContext> options, DateTime now,
        ReservationStatus status)
    {
        await using var context = new ECafeDbContext(options);
        context.Reservations.Add(new Reservation
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4, PeopleCount = 2,
            StatusId = StatusIds.Reservation(status), ReservedAt = now.AddHours(1),
            HoldExpiresAt = now.AddMinutes(-1), RestaurantResponseExpiresAt = now.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.PendingPayment, ReservationStatus.PaymentSubmitted)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction, ReservationStatus.Confirmed)]
    public async Task StatusChangedAfterSelectionIsPreserved(ReservationStatus initialStatus, ReservationStatus newStatus)
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now, initialStatus);
        await using var worker = new ECafeDbContext(options);
        var repository = new ReservationRepository(worker);
        var candidate = Assert.Single(await repository.GetPendingExpiryCandidatesAsync(now, 100));
        Assert.Empty(worker.ChangeTracker.Entries<Reservation>());

        await using (var otherRequest = new ECafeDbContext(options))
        {
            var reservation = await otherRequest.Reservations.SingleAsync();
            reservation.StatusId = StatusIds.Reservation(newStatus);
            otherRequest.ReservationStatusHistory.Add(new ReservationStatusHistory
            {
                ReservationId = reservation.Id, FromStatusId = StatusIds.Reservation(initialStatus),
                ToStatusId = reservation.StatusId, ChangedAt = now, Reason = "Other request completed."
            });
            await otherRequest.SaveChangesAsync();
        }

        Assert.False(await repository.TryExpirePendingReservationAsync(candidate, now));
        Assert.Equal(StatusIds.Reservation(newStatus), await worker.Reservations.Select(r => r.StatusId).SingleAsync());
        var history = Assert.Single(await worker.ReservationStatusHistory.ToListAsync());
        Assert.Equal(StatusIds.Reservation(newStatus), history.ToStatusId);
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction)]
    public async Task DeadlineExtendedAfterSelectionPreventsExpiry(ReservationStatus status)
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now, status);
        await using var worker = new ECafeDbContext(options);
        var repository = new ReservationRepository(worker);
        var candidate = Assert.Single(await repository.GetPendingExpiryCandidatesAsync(now, 100));

        await using (var otherRequest = new ECafeDbContext(options))
        {
            var reservation = await otherRequest.Reservations.SingleAsync();
            if (status == ReservationStatus.PendingPayment)
                reservation.HoldExpiresAt = now.AddMinutes(15);
            else
                reservation.RestaurantResponseExpiresAt = now.AddMinutes(15);
            await otherRequest.SaveChangesAsync();
        }

        Assert.False(await repository.TryExpirePendingReservationAsync(candidate, now));
        Assert.Equal(StatusIds.Reservation(status), await worker.Reservations.Select(r => r.StatusId).SingleAsync());
        Assert.Empty(await worker.ReservationStatusHistory.ToListAsync());
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction)]
    public async Task EligibleReservationExpiresOnceAndWritesOneHistoryEntry(ReservationStatus status)
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now, status);
        await using var worker = new ECafeDbContext(options);
        var repository = new ReservationRepository(worker);
        var candidate = Assert.Single(await repository.GetPendingExpiryCandidatesAsync(now, 100));

        Assert.True(await repository.TryExpirePendingReservationAsync(candidate, now));
        Assert.False(await repository.TryExpirePendingReservationAsync(candidate, now));
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Expired), await worker.Reservations.Select(r => r.StatusId).SingleAsync());
        var history = Assert.Single(await worker.ReservationStatusHistory.ToListAsync());
        Assert.Equal(StatusIds.Reservation(status), history.FromStatusId);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Expired), history.ToStatusId);
        Assert.Equal(now, history.ChangedAt);
    }

    [Fact]
    public async Task BatchLimitAndCandidateIdentityAreRespected()
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now, ReservationStatus.PendingPayment);
        await using var worker = new ECafeDbContext(options);
        var repository = new ReservationRepository(worker);

        Assert.Empty(await repository.GetPendingExpiryCandidatesAsync(now, 0));
        Assert.Empty(await repository.GetPendingExpiryCandidatesAsync(now, -1));
        Assert.False(await repository.TryExpirePendingReservationAsync(new(1, 99, 3), now));
        Assert.False(await repository.TryExpirePendingReservationAsync(new(1, 2, 99), now));
        Assert.Empty(await worker.ReservationStatusHistory.ToListAsync());
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment, true)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction, false)]
    public async Task NullDeadlinePreservesExistingExpiryPolicy(ReservationStatus status, bool shouldExpire)
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now, status);
        await using var worker = new ECafeDbContext(options);
        var reservation = await worker.Reservations.SingleAsync();
        reservation.HoldExpiresAt = null;
        reservation.RestaurantResponseExpiresAt = null;
        await worker.SaveChangesAsync();
        var repository = new ReservationRepository(worker);

        Assert.Equal(shouldExpire ? 1 : 0, (await repository.GetPendingExpiryCandidatesAsync(now, 100)).Count);
        Assert.Equal(shouldExpire, await repository.TryExpirePendingReservationAsync(new(1, 2, 3), now));
    }
}
