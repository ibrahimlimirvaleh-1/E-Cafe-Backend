using ECafe.Application.Repositories.File;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.File
{
    public class FileRepository : BaseRepository<Domain.Entities.File>, IFileRepository
    {
        public FileRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
