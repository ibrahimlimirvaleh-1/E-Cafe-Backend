using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.File
{
    public interface IFileRepository : IBaseRepository<Domain.Entities.File>
    {
        Task<Domain.Entities.File?> GetAttachableByIdAsync(int fileId);

        Task<List<Domain.Entities.File>> GetAttachableByIdsAsync(IEnumerable<int> fileIds);

        Task<Domain.Entities.File?> GetWithUsageByIdAsync(int fileId);

        Task<Domain.Entities.File?> GetWithUsageByTokenAsync(string token);

        Task<Domain.Entities.File?> GetPublicByTokenAsync(string token);

        Task<bool> IsAttachedAsync(int fileId);

        Task<List<Domain.Entities.File>> GetUnattachedOlderThanAsync(DateTime cutoffUtc, int take);
    }
}
