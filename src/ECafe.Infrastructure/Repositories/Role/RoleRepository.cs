using ECafe.Application.Repositories.Role;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.Role
{
    public class RoleRepository : BaseRepository<Domain.Entities.Role>, IRoleRepository
    {
        public RoleRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
