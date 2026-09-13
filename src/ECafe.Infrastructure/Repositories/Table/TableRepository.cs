using ECafe.Application.Repositories.Table;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Table
{
    public class TableRepository : BaseRepository<Domain.Entities.Table>, ITableRepository
    {
        private static readonly int OpenTableSessionStatusId = StatusIds.TableSession(TableSessionStatus.Open);

        private readonly ECafeDbContext _context;

        public TableRepository(ECafeDbContext context) : base(context)
        {
            _context = context;
        }

        public Task<bool> HasTableWithoutOpenSessionAsync(int restaurantId)
        {
            return Query()
                .AnyAsync(t =>
                    t.RestaurantId == restaurantId &&
                    t.IsActive &&
                    !t.TableSessions.Any(ts =>
                        ts.RestaurantId == restaurantId &&
                        ts.StatusId == OpenTableSessionStatusId &&
                        ts.ClosedAt == null));
        }

        public async Task<bool> HasAvailableTableForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var reservationInterval = await GetReservationIntervalUtcAsync(restaurantId, reservedAt);
            if (reservationInterval is null)
                return false;

            return await BuildAvailableTablesForReservationQuery(restaurantId, reservationInterval.Value)
                .AnyAsync();
        }

        public async Task<List<Domain.Entities.Table>> GetAvailableTablesForReservationAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var reservationInterval = await GetReservationIntervalUtcAsync(restaurantId, reservedAt);
            if (reservationInterval is null)
                return [];

            return await BuildAvailableTablesForReservationQuery(restaurantId, reservationInterval.Value)
                .OrderBy(t => t.TableNo)
                .ToListAsync();
        }

        private IQueryable<Domain.Entities.Table> BuildAvailableTablesForReservationQuery(
            int restaurantId,
            ReservationIntervalUtc reservationInterval)
        {
            return Query()
                .Where(t =>
                    t.RestaurantId == restaurantId &&
                    t.IsActive &&
                    !t.TableSessions.Any(ts =>
                        ts.RestaurantId == restaurantId &&
                        ts.StatusId == OpenTableSessionStatusId &&
                        ts.ClosedAt == null) &&
                    !t.Reservations.Any(r =>
                        r.RestaurantId == restaurantId &&
                        r.TableId == t.Id &&
                        r.Status.BlocksTableAvailability &&
                        r.ReservedAt >= reservationInterval.StartsAtUtc &&
                        r.ReservedAt < reservationInterval.EndsAtUtc));
        }

        private async Task<ReservationIntervalUtc?> GetReservationIntervalUtcAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var workingHours = await _context.RestaurantWorkingHours
                .AsNoTracking()
                .Where(wh => wh.RestaurantId == restaurantId && !wh.IsClosed)
                .ToListAsync();

            var reservationTimeContext = ReservationTimeContext.From(reservedAt);
            var matchingWorkingHour = workingHours.FirstOrDefault(workingHour =>
                ContainsReservationTime(workingHour, reservationTimeContext));

            return matchingWorkingHour is null
                ? null
                : BuildReservationIntervalUtc(matchingWorkingHour, reservationTimeContext);
        }

        private static bool ContainsReservationTime(
            Domain.Entities.RestaurantWorkingHour workingHour,
            ReservationTimeContext reservationTimeContext)
        {
            return IsOvernight(workingHour)
                ? ContainsReservationTimeInOvernightInterval(workingHour, reservationTimeContext)
                : ContainsReservationTimeInSameDayInterval(workingHour, reservationTimeContext);
        }

        private static bool ContainsReservationTimeInSameDayInterval(
            Domain.Entities.RestaurantWorkingHour workingHour,
            ReservationTimeContext reservationTimeContext)
        {
            return workingHour.DayOfWeek == reservationTimeContext.DayOfWeek &&
                   reservationTimeContext.Time >= workingHour.OpensAt &&
                   reservationTimeContext.Time < workingHour.ClosesAt;
        }

        private static bool ContainsReservationTimeInOvernightInterval(
            Domain.Entities.RestaurantWorkingHour workingHour,
            ReservationTimeContext reservationTimeContext)
        {
            return
                (workingHour.DayOfWeek == reservationTimeContext.DayOfWeek &&
                 reservationTimeContext.Time >= workingHour.OpensAt) ||
                (workingHour.DayOfWeek == reservationTimeContext.PreviousDayOfWeek &&
                 reservationTimeContext.Time < workingHour.ClosesAt);
        }

        private static ReservationIntervalUtc BuildReservationIntervalUtc(
            Domain.Entities.RestaurantWorkingHour workingHour,
            ReservationTimeContext reservationTimeContext)
        {
            var startDate = GetIntervalStartDate(workingHour, reservationTimeContext);
            var endDate = IsOvernight(workingHour)
                ? startDate.AddDays(1)
                : startDate;

            return new ReservationIntervalUtc(
                ToUtc(startDate, workingHour.OpensAt, reservationTimeContext.Offset),
                ToUtc(endDate, workingHour.ClosesAt, reservationTimeContext.Offset));
        }

        private static DateOnly GetIntervalStartDate(
            Domain.Entities.RestaurantWorkingHour workingHour,
            ReservationTimeContext reservationTimeContext)
        {
            return workingHour.DayOfWeek == reservationTimeContext.PreviousDayOfWeek &&
                   reservationTimeContext.Time < workingHour.ClosesAt
                ? reservationTimeContext.Date.AddDays(-1)
                : reservationTimeContext.Date;
        }

        private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeSpan offset)
        {
            return new DateTimeOffset(date.ToDateTime(time), offset).UtcDateTime;
        }

        private static bool IsOvernight(Domain.Entities.RestaurantWorkingHour workingHour)
        {
            return workingHour.OpensAt > workingHour.ClosesAt;
        }

        private readonly record struct ReservationIntervalUtc(DateTime StartsAtUtc, DateTime EndsAtUtc);

        private readonly record struct ReservationTimeContext(
            DateOnly Date,
            TimeOnly Time,
            DayOfWeek DayOfWeek,
            DayOfWeek PreviousDayOfWeek,
            TimeSpan Offset)
        {
            public static ReservationTimeContext From(DateTimeOffset reservedAt)
            {
                return new ReservationTimeContext(
                    DateOnly.FromDateTime(reservedAt.DateTime),
                    TimeOnly.FromDateTime(reservedAt.DateTime),
                    reservedAt.DayOfWeek,
                    reservedAt.AddDays(-1).DayOfWeek,
                    reservedAt.Offset);
            }
        }
    }
}
