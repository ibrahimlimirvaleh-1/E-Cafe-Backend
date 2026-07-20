using AutoMapper;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.Table.Abstract;

namespace ECafe.Application.Services.Table.Concrete
{
    public class TableManager : ITableService
    {
        private readonly ITableRepository _tableRepository;
        private readonly IRestaurantContractService _restaurantContractService;
        private readonly IMapper _mapper;

        public TableManager(
            ITableRepository tableRepository,
            IRestaurantContractService restaurantContractService,
            IMapper mapper)
        {
            _tableRepository = tableRepository;
            _restaurantContractService = restaurantContractService;
            _mapper = mapper;
        }

        public async Task<int> CreateAsync(CreateTableRequest request)
        {
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(request.RestaurantId);

            var existTable = await _tableRepository.CheckExistAsync(x => x.TableNo == request.TableNo && x.RestaurantId == request.RestaurantId);

            if (existTable)
                throw new InvalidOperationException("Table with the same number already exists.");

            var table = _mapper.Map<Domain.Entities.Table>(request);

            await _tableRepository.Add(table);
            await _tableRepository.SaveChangesAsync();

            return table.Id;
        }
    }
}
