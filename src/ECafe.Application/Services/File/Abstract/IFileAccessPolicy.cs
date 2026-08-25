namespace ECafe.Application.Services.FileAccess.Abstract
{
    public interface IFileAccessPolicy
    {
        void EnsureCurrentUserCanAccess(Domain.Entities.File file);
    }
}
