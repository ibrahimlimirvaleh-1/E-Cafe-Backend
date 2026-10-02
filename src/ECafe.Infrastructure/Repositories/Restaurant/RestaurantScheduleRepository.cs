using ECafe.Application.Repositories.Restaurant;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Restaurant;

public sealed class RestaurantScheduleRepository(ECafeDbContext context) : IRestaurantScheduleRepository
{
    public async Task AcquireLocksAsync(int restaurantId, CancellationToken ct)
    {
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({restaurantId}, {0})", ct);
            var tables = await context.Tables.Where(t => t.RestaurantId == restaurantId)
                .OrderBy(t => t.Id).Select(t => t.Id).ToListAsync(ct);
            foreach (var tableId in tables)
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({restaurantId}, {tableId})", ct);
        }
    }

    public Task<Domain.Entities.Restaurant?> GetRestaurantAsync(int id, CancellationToken ct)
        => context.Restaurants.Include(r => r.WorkingHours).SingleOrDefaultAsync(r => r.Id == id && r.IsActive, ct);

    private IQueryable<RestaurantScheduleChange> Changes(int id)
        => context.RestaurantScheduleChanges.Where(c => c.RestaurantId == id)
            .Include(c => c.Restaurant).Include(c => c.Consents).ThenInclude(c => c.Reservation).ThenInclude(r => r!.Table)
            .Include(c => c.Consents).ThenInclude(c => c.Reservation).ThenInclude(r => r!.CustomerUser)
            .Include(c => c.Consents).ThenInclude(c => c.Reservation).ThenInclude(r => r!.ArrivalAdjustment)
            .Include(c => c.Consents).ThenInclude(c => c.TableSession).ThenInclude(s => s!.Table);

    public Task<RestaurantScheduleChange?> GetPendingAsync(int id, CancellationToken ct)
        => Changes(id).SingleOrDefaultAsync(c => c.State == ScheduleChangeState.Pending, ct);
    public Task<RestaurantScheduleChange?> GetLatestAsync(int id, CancellationToken ct)
        => Changes(id).OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);

    public Task<List<Domain.Entities.Reservation>> GetActiveReservationsAsync(int id, DateTime now, CancellationToken ct)
    {
        var awaiting = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
        var pending = StatusIds.Reservation(ReservationStatus.PendingPayment);
        var confirmed = StatusIds.Reservation(ReservationStatus.Confirmed);
        var submitted = StatusIds.Reservation(ReservationStatus.PaymentSubmitted);
        var seated = StatusIds.Reservation(ReservationStatus.Seated);
        return context.Reservations.Include(r => r.Table).Include(r => r.CustomerUser).Include(r => r.ArrivalAdjustment)
            .Where(r => r.RestaurantId == id &&
                (r.StatusId == seated ||
                 r.StatusId == confirmed && (r.NoShowDeadlineAt > now || r.ArrivalAdjustment!.DecisionExpiresAt > now) ||
                 r.ReservedAt > now && (r.StatusId == submitted ||
                    r.StatusId == awaiting && r.RestaurantResponseExpiresAt > now ||
                    r.StatusId == pending && (r.HoldExpiresAt == null || r.HoldExpiresAt > now))))
            .ToListAsync(ct);
    }

    public Task<List<Domain.Entities.TableSession>> GetOpenSessionsAsync(int id, CancellationToken ct)
        => context.TableSessions.Include(s => s.Table)
            .Where(s => s.RestaurantId == id && s.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && s.ClosedAt == null)
            .ToListAsync(ct);

    public Task<RestaurantScheduleConsent?> GetCustomerConsentAsync(int id, int userId, CancellationToken ct)
        => context.RestaurantScheduleConsents.AsNoTracking().Include(c => c.ScheduleChange).ThenInclude(c => c.Restaurant)
            .Include(c => c.Reservation).ThenInclude(r => r!.ArrivalAdjustment)
            .Where(c => c.ReservationId == id && c.Reservation!.CustomerUserId == userId)
            .OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
    public async Task AddAsync(RestaurantScheduleChange change, CancellationToken ct)
        => await context.RestaurantScheduleChanges.AddAsync(change, ct);
    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
