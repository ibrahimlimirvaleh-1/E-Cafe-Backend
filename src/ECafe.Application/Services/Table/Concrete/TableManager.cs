using AutoMapper;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Table.Concrete
{
    public class TableManager : BaseManager, ITableService
    {
        private readonly ITableRepository _tableRepository;
        private readonly IRestaurantContractService _restaurantContractService;
        private readonly IMapper _mapper;

        public TableManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            ITableRepository tableRepository,
            IRestaurantContractService restaurantContractService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _tableRepository = tableRepository;
            _restaurantContractService = restaurantContractService;
            _mapper = mapper;
        }

        public async Task<int> CreateAsync(CreateTableRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required.");

            EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
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
