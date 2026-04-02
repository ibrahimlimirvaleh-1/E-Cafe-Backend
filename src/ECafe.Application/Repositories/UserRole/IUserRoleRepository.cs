using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.UserRole
{
    public interface IUserRoleRepository : IBaseRepository<Domain.Entities.UserRole>
    {
        public Task<List<Domain.Entities.UserRole>> GetByUserIdAsync(int userId);
    }
}
