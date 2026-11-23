using ECafe.Domain.Entities;
using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.ProductAnnouncements;

public interface IProductAnnouncementRepository : IBaseRepository<ProductAnnouncement>
{
    Task AcquireWriteLockAsync(int id, CancellationToken cancellationToken);
    Task<ProductAnnouncement?> FindTrackedAsync(int id, CancellationToken cancellationToken);
    Task<List<ProductAnnouncement>> PendingAsync(int userId, int roleId, string[] destinations, DateTime nowUtc, int take, CancellationToken cancellationToken);
    Task<List<ProductAnnouncement>> AdminPageAsync(int? beforeId, int take, CancellationToken cancellationToken);
    Task<bool> HasReceiptAsync(int userId, int roleId, int id, int version, CancellationToken cancellationToken);
    Task AddReceiptAsync(ProductAnnouncementReceipt receipt, CancellationToken cancellationToken);
}
