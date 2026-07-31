using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.InventoryMovementType
{
    public interface IInventoryMovementTypeRepository : IBaseRepository<Domain.Entities.InventoryMovementType>
    {
        Task<Domain.Entities.InventoryMovementType?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default);
    }
}
