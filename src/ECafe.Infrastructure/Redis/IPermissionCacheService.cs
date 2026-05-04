namespace ECafe.Infrastructure.Redis
{
    public interface IPermissionCacheService
    {
        Task<List<int>> GetPermissionsAsync(int roleId);
        Task InvalidateAsync(int roleId);
    }
}
