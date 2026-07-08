using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.User
{
    public interface IUserRepository : IBaseRepository<Domain.Entities.User>
    {
        Task<Domain.Entities.User?> GetByEmailAsync(string email);

        Task<Domain.Entities.User?> GetByEmailTrackedAsync(string email);

        Task<List<Domain.Entities.User>> GetByRestaurantIdAsync(int restaurantId);

        IQueryable<Domain.Entities.User> GetUsersForList(int? restaurantId);

        Task<Domain.Entities.User?> GetProfileByIdAsync(int userId);

        Task<Domain.Entities.User?> GetProfileByIdTrackedAsync(int userId);

        Task<Domain.Entities.User?> GetStaffDetailAsync(int restaurantId, int staffId);

        Task<Domain.Entities.User?> GetProfileConflictAsync(int userId, string email, string phone);

    }
}
