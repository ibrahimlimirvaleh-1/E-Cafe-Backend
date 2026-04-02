using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserRole
{
    public interface IUserRoleRepository : IBaseRepository<Domain.Entities.UserRole>
    {
        Task<Domain.Entities.UserRole?> GetSingleByUserIdAsync(int userId);
    }
}
