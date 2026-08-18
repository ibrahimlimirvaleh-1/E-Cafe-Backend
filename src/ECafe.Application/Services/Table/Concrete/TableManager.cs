using AutoMapper;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

        public async Task<int> CreateAsync(int restaurantId, CreateTableRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required.");

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId);

            await EnsureTableNumberIsUniqueAsync(restaurantId, request.TableNo);

            var table = _mapper.Map<Domain.Entities.Table>(request);
            table.RestaurantId = restaurantId;

            await _tableRepository.Add(table);
            await _tableRepository.SaveChangesAsync();

            return table.Id;
        }

        public async Task<List<TableResponse>> GetByRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var tables = await _tableRepository
                .Query(x => x.RestaurantId == restaurantId && x.IsActive)
                .OrderBy(x => x.TableNo)
                .Select(x => new TableResponse
                {
                    Id = x.Id,
                    RestaurantId = x.RestaurantId,
                    TableNo = x.TableNo,
                    Name = x.Name,
                    Capacity = x.Capacity,
                    IsActive = x.IsActive,
                    IsEmpty = x.IsEmpty
                })
                .ToListAsync();

            return tables;
        }

        private async Task EnsureTableNumberIsUniqueAsync(int restaurantId, int tableNo)
        {
            var tableExists = await _tableRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.TableNo == tableNo);

            if (tableExists)
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo });
        }
    }
}
