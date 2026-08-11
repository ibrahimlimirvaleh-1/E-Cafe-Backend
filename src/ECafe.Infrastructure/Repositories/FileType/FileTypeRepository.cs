using ECafe.Application.Repositories.FileType;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.FileType
{
    public class FileTypeRepository : BaseRepository<Domain.Entities.FileType>, IFileTypeRepository
    {
        public FileTypeRepository(ECafeDbContext context) : base(context)
        {
        }

        public Task<Domain.Entities.FileType?> GetByTypeAsync(
            FileTypeCode fileTypeCode,
            CancellationToken cancellationToken = default)
            => Query().FirstOrDefaultAsync(fileType => fileType.Id == (int)fileTypeCode, cancellationToken);

        public Task<Domain.Entities.FileType?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
            => Query().FirstOrDefaultAsync(fileType => fileType.Code == code, cancellationToken);
    }
}
