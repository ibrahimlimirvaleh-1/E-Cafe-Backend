using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.RestaurantContract
{
    public class RestaurantContractRepository
        : BaseRepository<Domain.Entities.RestaurantContract>, IRestaurantContractRepository
    {
        public RestaurantContractRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<List<Domain.Entities.RestaurantContract>> GetByRestaurantAsync(int restaurantId)
            => Query(x => x.RestaurantId == restaurantId)
                .Include(x => x.Status)
                .Include(x => x.File)
                .Include(x => x.SignedByUser)
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

        public Task<Domain.Entities.RestaurantContract?> GetActiveByRestaurantAsync(int restaurantId)
            => Query(x => x.RestaurantId == restaurantId && x.StatusId == ActiveStatusId)
                .Include(x => x.Status)
                .Include(x => x.File)
                .Include(x => x.SignedByUser)
                .FirstOrDefaultAsync();

        public Task<Domain.Entities.RestaurantContract?> GetTrackedByRestaurantAsync(int restaurantId, int contractId)
            => QueryTracked(x => x.RestaurantId == restaurantId && x.Id == contractId)
                .FirstOrDefaultAsync();

        public Task<bool> HasActiveContractAsync(int restaurantId)
            => Query(x => x.RestaurantId == restaurantId && x.StatusId == ActiveStatusId)
                .AnyAsync();

        private static int ActiveStatusId =>
            ((int)StatusType.Contract * 1000) + (int)ContractStatus.Active;
    }
}
