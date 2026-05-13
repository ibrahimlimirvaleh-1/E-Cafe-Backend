using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services.Table.Abstract;

namespace ECafe.Application.Services.Table.Concrete
{
    public class TableManager : ITableService
    {
        private readonly ITableRepository _tableRepository;

        public TableManager(ITableRepository tableRepository)
        {
            _tableRepository = tableRepository;
        }

        public async Task<int> CreateAsync(CreateTableRequest request)
        {
            var existTable = await _tableRepository.CheckExistAsync(x => x.TableNo == request.TableNo);

            if (existTable)
                throw new InvalidOperationException("Table with the same number already exists.");

            var table = new Domain.Entities.Table
            {
                RestaurantId = request.RestaurantId,
                Name = request.Name,
                TableNo = request.TableNo,
                Capacity = request.Capacity,
                IsActive = true,
                IsEmpty = true
            };

            await _tableRepository.Add(table);
            await _tableRepository.SaveChangesAsync();

            return table.Id;
        }
    }
}
