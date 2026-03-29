using ECafe.Application.Repositories.User;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.User
{
    public class UserRepository : BaseRepository<Domain.Entities.User>, IUserRepository
    {
        public UserRepository(ECafeDbContext context) : base(context)
        {
        }

        public async Task<Domain.Entities.User> GetByEmailAsync(string email)
        {
            return await Query()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.Email == email).FirstOrDefaultAsync();
        }
    }
}
