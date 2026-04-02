using ECafe.Application.Repositories.UserRole;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.UserRole
{
    public class UserRoleRepository : BaseRepository<Domain.Entities.UserRole>, IUserRoleRepository
    {
        public UserRoleRepository(ECafeDbContext context) : base(context)
        {
        }

        public async Task<Domain.Entities.UserRole?> GetSingleByUserIdAsync(int userId)
        {
            return await Query()
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }
    }
}
