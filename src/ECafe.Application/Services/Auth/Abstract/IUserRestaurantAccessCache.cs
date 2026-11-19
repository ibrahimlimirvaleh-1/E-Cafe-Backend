namespace ECafe.Application.Services.Auth.Abstract;

/// <summary>Restoran rolunu cari bazadan yoxlayır; əvvəlki ad və köhnə açar təmizliyi mövcud çağırışlar üçün saxlanır.</summary>
public interface IUserRestaurantAccessCache
{
    Task<int?> GetActiveRoleIdAsync(int userId, int restaurantId);

    Task InvalidateAsync(int userId, int restaurantId);
}
