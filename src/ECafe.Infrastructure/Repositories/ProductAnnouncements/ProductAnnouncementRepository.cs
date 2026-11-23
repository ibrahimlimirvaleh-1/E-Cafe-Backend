using ECafe.Application.Repositories.ProductAnnouncements;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ProductAnnouncements;

public sealed class ProductAnnouncementRepository(ECafeDbContext context)
    : BaseRepository<ProductAnnouncement>(context), IProductAnnouncementRepository
{
    private const int WriteLockNamespace = -19200;

    /// <summary>Publish/edit/ack qərarlarını əvvəlki ümumi parametrli PostgreSQL kilid komponenti ilə ardıcıllaşdırır.</summary>
    public Task AcquireWriteLockAsync(int id, CancellationToken cancellationToken)
        => Context.TransactionLocks.LockAdvisoryAsync(WriteLockNamespace, id, cancellationToken);

    public Task<ProductAnnouncement?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => QueryTracked(value => value.Id == id).Include(value => value.Texts).Include(value => value.Roles)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);

    public Task<List<ProductAnnouncement>> PendingAsync(int userId, int roleId, string[] destinations, DateTime nowUtc, int take, CancellationToken cancellationToken)
        => Query(value => value.PublishedAtUtc != null && value.PublishedAtUtc <= nowUtc &&
                (value.ExpiresAtUtc == null || value.ExpiresAtUtc > nowUtc) && destinations.Contains(value.Destination) &&
                value.Roles.Any(role => role.RoleId == roleId) && !Context.ProductAnnouncementReceipts.Any(receipt =>
                    receipt.UserId == userId && receipt.RoleId == roleId && receipt.AnnouncementId == value.Id && receipt.Version == value.Version))
            .OrderByDescending(value => value.PublishedAtUtc).ThenByDescending(value => value.Id).Take(take)
            .Include(value => value.Texts).Include(value => value.Roles).AsSingleQuery().ToListAsync(cancellationToken);

    public Task<List<ProductAnnouncement>> AdminPageAsync(int? beforeId, int take, CancellationToken cancellationToken)
        => Query(value => !beforeId.HasValue || value.Id < beforeId.Value).OrderByDescending(value => value.Id).Take(take)
            .Include(value => value.Texts).Include(value => value.Roles).AsSplitQuery().ToListAsync(cancellationToken);

    public Task<bool> HasReceiptAsync(int userId, int roleId, int id, int version, CancellationToken cancellationToken)
        => Context.ProductAnnouncementReceipts.AnyAsync(value => value.UserId == userId && value.RoleId == roleId &&
            value.AnnouncementId == id && value.Version == version, cancellationToken);

    public async Task AddReceiptAsync(ProductAnnouncementReceipt receipt, CancellationToken cancellationToken)
        => await Context.ProductAnnouncementReceipts.AddAsync(receipt, cancellationToken);
}
