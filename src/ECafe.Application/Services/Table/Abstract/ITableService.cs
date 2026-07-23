using ECafe.Application.DTOs.Table;

namespace ECafe.Application.Services.Table.Abstract
{
    public interface ITableService
    {
        Task<int> CreateAsync(CreateTableRequest request);

        Task<List<TableResponse>> GetByRestaurantAsync(int restaurantId);
    }
}
