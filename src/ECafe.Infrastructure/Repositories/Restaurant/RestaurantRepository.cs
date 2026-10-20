using ECafe.Application.Repositories.Restaurant;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.Restaurant
{
    public class RestaurantRepository : BaseRepository<Domain.Entities.Restaurant>, IRestaurantRepository
    {
        public RestaurantRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task AcquireScheduleLockAsync(int restaurantId, CancellationToken cancellationToken = default)
            => Context.Database.IsRelational()
                ? Context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({restaurantId}, {0})", cancellationToken)
                : Task.CompletedTask;

        public Task<bool> HasPendingScheduleChangeAsync(int restaurantId, CancellationToken cancellationToken = default)
            => Context.RestaurantScheduleChanges.AnyAsync(c => c.RestaurantId == restaurantId &&
                c.State == ScheduleChangeState.Pending, cancellationToken);

        public IQueryable<Domain.Entities.Restaurant> GetActiveRestaurants()
        {
            var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);

            return Query()
                .Include(r => r.RestaurantGroup)
                .Include(r => r.Files)
                .Include(r => r.WorkingHours)
                .Include(r => r.Categories)
                    .ThenInclude(c => c.Items)
                .AsSplitQuery()
                .Where(r => r.IsActive && r.Contracts.Any(c => c.StatusId == activeContractStatusId));
        }

        public IQueryable<Domain.Entities.Restaurant> GetRestaurantsForList()
        {
            return Query()
                .Include(r => r.RestaurantGroup)
                .Include(r => r.Files)
                .Include(r => r.Contracts)
                .Include(r => r.WorkingHours)
                .Include(r => r.Categories)
                    .ThenInclude(c => c.Items)
                .AsSplitQuery()
                .Where(r => r.IsActive);
        }

        public Task<Domain.Entities.Restaurant?> GetRestaurantInfoAsync(int id)
        {
            return Query()
                .Include(r => r.RestaurantGroup)
                .Include(r => r.Files)
                .Include(r => r.WorkingHours)
                .Include(r => r.DepositRules)
                .Include(r => r.Tables)
                    .ThenInclude(t => t.TableSessions)
                .Include(r => r.Categories)
                    .ThenInclude(c => c.Items)
                        .ThenInclude(i => i.File)
                .Include(r => r.UserRestaurants)
                    .ThenInclude(ur => ur.User)
                        .ThenInclude(u => u.Role)
                .Include(r => r.UserRestaurants)
                    .ThenInclude(ur => ur.User)
                        .ThenInclude(u => u.File)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        }

        public Task<Domain.Entities.Restaurant?> GetPublicRestaurantInfoAsync(int id)
        {
            var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);

            return Query()
                .Include(r => r.RestaurantGroup)
                .Include(r => r.Files)
                .Include(r => r.WorkingHours)
                .Include(r => r.Tables)
                    .ThenInclude(t => t.TableSessions)
                .Include(r => r.Categories)
                    .ThenInclude(c => c.Items)
                        .ThenInclude(i => i.File)
                .Include(r => r.UserRestaurants)
                    .ThenInclude(ur => ur.User)
                        .ThenInclude(u => u.Role)
                .Include(r => r.UserRestaurants)
                    .ThenInclude(ur => ur.User)
                        .ThenInclude(u => u.File)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.IsActive &&
                    r.Contracts.Any(c => c.StatusId == activeContractStatusId));
        }

        public Task<bool> HasRestaurantActiveContractAsync(int id)
        {
            var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);

            return Query()
                .AnyAsync(r => r.Id == id && r.Contracts.Any(c => c.StatusId == activeContractStatusId));
        }

        public async Task<bool> IsRestaurantOpenAsync(int restaurantId, DateTimeOffset reservedAt)
        {
            var restaurant = await Query(r => r.Id == restaurantId && r.IsActive)
                .Include(r => r.WorkingHours)
                .SingleOrDefaultAsync();

            if (restaurant is null)
                return false;

            var pending = await Context.RestaurantScheduleChanges.AsNoTracking()
                .Where(c => c.RestaurantId == restaurantId && c.State == ScheduleChangeState.Pending)
                .Select(c => c.ProposedHoursJson).SingleOrDefaultAsync();
            if (pending != null && ScheduleTerms.IsAffected(restaurant.WorkingHours,
                    ScheduleTerms.Deserialize(pending), restaurant.TimeZone, reservedAt.UtcDateTime, null, out _))
                throw new BusinessRuleException(ErrorCode.ScheduleBookingPaused);
            var restaurantLocalTime = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
                reservedAt.ToUniversalTime(),
                restaurant.TimeZone);

            return RestaurantWorkingHoursCalculator.TryGetActiveInterval(
                restaurant.WorkingHours,
                restaurantLocalTime,
                out _);
        }
    }
}
