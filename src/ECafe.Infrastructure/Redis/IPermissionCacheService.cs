namespace ECafe.Infrastructure.Redis
{
    /// <summary>Rol icazələri cari bazadan yoxlanır; köhnə keşin silinməsi təhlükəsizlik qərarı üçün şərt deyil.</summary>
    public interface IPermissionCacheService
    {
        Task<List<int>> GetPermissionsAsync(int roleId);
        Task InvalidateAsync(int roleId);
    }
}
