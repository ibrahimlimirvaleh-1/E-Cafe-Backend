using ECafe.Application.Repository;

namespace ECafe.Application.Repositories.Restaurant
{
    public interface IRestaurantRepository : IBaseRepository<Domain.Entities.Restaurant>
    {
        IQueryable<Domain.Entities.Restaurant> GetActiveRestaurants();
    }
}
