using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Table
{
    public interface ITableRepository : IBaseRepository<Domain.Entities.Table>
    {
        Task<bool> HasTableWithoutOpenSessionAsync(int restaurantId);

        Task<bool> HasAvailableTableForReservationAsync(int restaurantId, DateTimeOffset reservedAt);

        Task<List<Domain.Entities.Table>> GetAvailableTablesForReservationAsync(int restaurantId, DateTimeOffset reservedAt);

        Task<List<ReservationTableAvailability>> GetReservationTableAvailabilityAsync(
            int restaurantId,
            DateTimeOffset reservedAt);

        Task<ReservationTableAvailability?> GetReservationTableAvailabilityAsync(
            int restaurantId,
            int tableId,
            DateTimeOffset reservedAt,
            int? excludedReservationId = null);

        Task<bool> HasOpenTableSessionAsync(int restaurantId, int tableId);

        Task AcquireReservationLockAsync(
            int restaurantId,
            int tableId,
            CancellationToken cancellationToken = default);

        Task<bool> IsTableAvailableForReservationAsync(
            int restaurantId,
            int tableId,
            DateTimeOffset reservedAt,
            int? excludedReservationId = null);

    }
}
