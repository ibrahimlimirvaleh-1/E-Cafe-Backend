using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.User
{
    public interface IUserRepository : IBaseRepository<Domain.Entities.User>
    {
        Task<Domain.Entities.User> GetByEmailAsync(string email);
    }
}
