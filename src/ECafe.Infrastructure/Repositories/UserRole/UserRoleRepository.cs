using ECafe.Application.Repositories.UserRole;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.UserRole
{
    public class UserRoleRepository : BaseRepository<Domain.Entities.UserRole>, IUserRoleRepository
    {
        public UserRoleRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
