using ECafe.Application.Repository;
using ECafe.Domain.Enums;

namespace ECafe.Application.Repositories.FileType
{
    public interface IFileTypeRepository : IBaseRepository<Domain.Entities.FileType>
    {
        Task<Domain.Entities.FileType?> GetByTypeAsync(FileTypeCode fileTypeCode, CancellationToken cancellationToken = default);

        Task<Domain.Entities.FileType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}
