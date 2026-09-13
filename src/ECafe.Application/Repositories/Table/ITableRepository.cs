using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Table
{
    public interface ITableRepository : IBaseRepository<Domain.Entities.Table>
    {
        Task<bool> HasTableWithoutOpenSessionAsync(int restaurantId);

        Task<bool> HasAvailableTableForReservationAsync(int restaurantId, DateTimeOffset reservedAt);

        Task<List<Domain.Entities.Table>> GetAvailableTablesForReservationAsync(int restaurantId, DateTimeOffset reservedAt);
    }
}
