using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Item
{
    public interface IItemRepository : IBaseRepository<Domain.Entities.Item>
    {
        Task LoadTranslationsAsync(Domain.Entities.Item owner, CancellationToken cancellationToken = default);
    }
}
