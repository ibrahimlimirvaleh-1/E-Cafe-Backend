using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Reservation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationNoShowRepositoryTests
{
    private static DbContextOptions<ECafeDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static async Task SeedAsync(DbContextOptions<ECafeDbContext> options, DateTime nowUtc)
    {
        await using var context = new ECafeDbContext(options);
        context.Reservations.Add(new Reservation
        {
            Id = 1,
            RestaurantId = 2,
            TableId = 3,
            CustomerUserId = 4,
            PeopleCount = 2,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            ReservedAt = nowUtc.AddHours(-1),
            NoShowDeadlineAt = nowUtc.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(ReservationStatus.Seated)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed)]
    public async Task StatusChangedAfterCandidateSelection_IsNotOverwritten(ReservationStatus newStatus)
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var workerContext = new ECafeDbContext(options);
        var repository = new ReservationRepository(workerContext);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(nowUtc, 100));
        Assert.Empty(workerContext.ChangeTracker.Entries<Reservation>());

        await using (var checkInContext = new ECafeDbContext(options))
        {
            var reservation = await checkInContext.Reservations.SingleAsync();
            reservation.StatusId = StatusIds.Reservation(newStatus);
            await checkInContext.SaveChangesAsync();
        }

        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));
        Assert.Equal(StatusIds.Reservation(newStatus),
            await workerContext.Reservations.Select(r => r.StatusId).SingleAsync());
        Assert.Empty(await workerContext.ReservationStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task SessionOpenedAfterCandidateSelection_PreventsNoShow()
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var workerContext = new ECafeDbContext(options);
        var repository = new ReservationRepository(workerContext);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(nowUtc, 100));

        await using (var sessionContext = new ECafeDbContext(options))
        {
            sessionContext.TableSessions.Add(new TableSession
            {
                RestaurantId = candidate.RestaurantId,
                TableId = candidate.TableId,
                ReservationId = candidate.ReservationId,
                StatusId = StatusIds.TableSession(TableSessionStatus.Open),
                OpenedAt = nowUtc
            });
            await sessionContext.SaveChangesAsync();
        }

        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));
        Assert.Empty(await workerContext.ReservationStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task DeadlineExtendedAfterCandidateSelection_PreventsNoShow()
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var workerContext = new ECafeDbContext(options);
        var repository = new ReservationRepository(workerContext);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(nowUtc, 100));

        await using (var updateContext = new ECafeDbContext(options))
        {
            var reservation = await updateContext.Reservations.SingleAsync();
            reservation.NoShowDeadlineAt = nowUtc.AddMinutes(10);
            await updateContext.SaveChangesAsync();
        }

        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));
    }

    [Fact]
    public async Task ArrivalRecordedAfterCandidateSelection_PreventsNoShowWithoutOpeningSession()
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var workerContext = new ECafeDbContext(options);
        var repository = new ReservationRepository(workerContext);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(nowUtc, 100));

        await using (var arrivalContext = new ECafeDbContext(options))
        {
            var reservation = await arrivalContext.Reservations.SingleAsync();
            reservation.ArrivedAt = nowUtc.AddMinutes(-2);
            reservation.ArrivedByUserId = 5;
            await arrivalContext.SaveChangesAsync();
        }

        Assert.Empty(await repository.GetNoShowCandidatesAsync(nowUtc, 100));
        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));
        Assert.Empty(await workerContext.TableSessions.ToListAsync());
    }

    [Fact]
    public async Task EligibleReservation_IsExpiredOnceWithOneHistoryEntry()
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var context = new ECafeDbContext(options);
        var repository = new ReservationRepository(context);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(nowUtc, 100));

        Assert.True(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));
        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, nowUtc));

        var reservation = await context.Reservations.SingleAsync();
        Assert.Equal(StatusIds.Reservation(ReservationStatus.NoShow), reservation.StatusId);
        Assert.Equal(nowUtc, reservation.NoShowAt);
        var history = Assert.Single(await context.ReservationStatusHistory.ToListAsync());
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), history.FromStatusId);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.NoShow), history.ToStatusId);
    }

    [Fact]
    public async Task CandidateWithDifferentTable_IsNotExpired()
    {
        var nowUtc = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, nowUtc);
        await using var context = new ECafeDbContext(options);
        var repository = new ReservationRepository(context);

        Assert.False(await repository.TryExpireNoShowReservationAsync(
            new ReservationNoShowCandidate(1, 2, 99), nowUtc));
    }

    [Fact]
    public async Task PendingConsentCreatedAfterCandidateSelection_PreventsNoShowUntilExpiry()
    {
        var now = DateTime.UtcNow;
        var options = CreateOptions();
        await SeedAsync(options, now);
        await using var context = new ECafeDbContext(options);
        var repository = new ReservationRepository(context);
        var candidate = Assert.Single(await repository.GetNoShowCandidatesAsync(now, 100));
        await using (var consentContext = new ECafeDbContext(options))
        {
            consentContext.ReservationArrivalAdjustments.Add(new()
            {
                ReservationId = 1, DecisionExpiresAt = now.AddMinutes(2)
            });
            await consentContext.SaveChangesAsync();
        }
        Assert.Empty(await repository.GetNoShowCandidatesAsync(now, 100));
        Assert.False(await repository.TryExpireNoShowReservationAsync(candidate, now));
        Assert.True(await repository.TryExpireNoShowReservationAsync(candidate, now.AddMinutes(2)));
        Assert.Single(await context.ReservationStatusHistory.ToListAsync());
    }
}
