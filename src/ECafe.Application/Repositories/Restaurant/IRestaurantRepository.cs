using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Restaurant
{
    public interface IRestaurantRepository : IBaseRepository<Domain.Entities.Restaurant>
    {
        IQueryable<Domain.Entities.Restaurant> GetActiveRestaurants();

        IQueryable<Domain.Entities.Restaurant> GetRestaurantsForList();

        Task<Domain.Entities.Restaurant?> GetRestaurantInfoAsync(int id);

        Task<Domain.Entities.Restaurant?> GetPublicRestaurantInfoAsync(int id);
    }
}
