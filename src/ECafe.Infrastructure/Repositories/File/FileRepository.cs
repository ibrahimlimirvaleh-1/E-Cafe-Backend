using ECafe.Application.Repositories.File;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.File
{
    public class FileRepository : BaseRepository<Domain.Entities.File>, IFileRepository
    {
        private readonly ECafeDbContext _context;

        public FileRepository(ECafeDbContext context) : base(context)
        {
            _context = context;
        }

        public Task<Domain.Entities.File?> GetAttachableByIdAsync(int fileId)
            => AttachableQuery()
                .FirstOrDefaultAsync(file => file.Id == fileId);

        public Task<List<Domain.Entities.File>> GetAttachableByIdsAsync(IEnumerable<int> fileIds)
        {
            var ids = fileIds.Distinct().ToList();
            return AttachableQuery()
                .Where(file => ids.Contains(file.Id))
                .ToListAsync();
        }

        public Task<Domain.Entities.File?> GetWithUsageByIdAsync(int fileId)
            => _context.Files
                .Include(file => file.FileType)
                .Include(file => file.Items)
                .Include(file => file.User)
                .Include(file => file.Restaurant)
                .Include(file => file.RestaurantContracts)
                .FirstOrDefaultAsync(file => file.Id == fileId);

        public Task<Domain.Entities.File?> GetWithUsageByTokenAsync(string token)
            => _context.Files
                .Include(file => file.FileType)
                .Include(file => file.Items)
                .Include(file => file.User)
                .Include(file => file.Restaurant)
                .Include(file => file.RestaurantContracts)
                .FirstOrDefaultAsync(file => file.Token == token);

        public Task<Domain.Entities.File?> GetPublicByTokenAsync(string token)
            => _context.Files
                .Include(file => file.FileType)
                .FirstOrDefaultAsync(file => file.Token == token);

        public Task<bool> IsAttachedAsync(int fileId)
            => _context.Files
                .AnyAsync(file =>
                    file.Id == fileId &&
                    (file.Items.Any() ||
                     file.User != null ||
                     file.RestaurantContracts.Any() ||
                     EF.Property<int?>(file, "RestaurantId") != null));

        public Task<List<Domain.Entities.File>> GetUnattachedOlderThanAsync(DateTime cutoffUtc, int take)
            => AttachableQuery()
                .Where(file => file.CreatedAt < cutoffUtc)
                .OrderBy(file => file.CreatedAt)
                .Take(take)
                .ToListAsync();

        private IQueryable<Domain.Entities.File> AttachableQuery()
            => _context.Files
                .Where(file =>
                    !file.Items.Any() &&
                    file.User == null &&
                    !file.RestaurantContracts.Any() &&
                    EF.Property<int?>(file, "RestaurantId") == null);
    }
}
