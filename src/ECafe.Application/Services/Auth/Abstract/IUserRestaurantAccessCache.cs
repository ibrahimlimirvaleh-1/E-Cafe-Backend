namespace ECafe.Application.Services.Auth.Abstract;

public interface IUserRestaurantAccessCache
{
    Task<int?> GetActiveRoleIdAsync(int userId, int restaurantId);

    Task InvalidateAsync(int userId, int restaurantId);
}
