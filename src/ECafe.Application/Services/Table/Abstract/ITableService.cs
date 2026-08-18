using ECafe.Application.DTOs.Table;

namespace ECafe.Application.Services.Table.Abstract
{
    public interface ITableService
    {
        Task<int> CreateAsync(int restaurantId, CreateTableRequest request);

        Task<TableResponse> UpdateAsync(int restaurantId, int tableId, UpdateTableRequest request);

        Task<TableResponse> DeactivateAsync(int restaurantId, int tableId);

        Task<TableResponse> DeleteAsync(int restaurantId, int tableId);

        Task<List<TableResponse>> GetByRestaurantAsync(int restaurantId);
    }
}
