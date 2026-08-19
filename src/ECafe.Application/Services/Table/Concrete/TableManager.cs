using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services;
using ECafe.Application.Services.AuditLog.Abstract;
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
        private readonly IAuditLogService _auditLogService;
        private readonly IMapper _mapper;

        public TableManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            ITableRepository tableRepository,
            IRestaurantContractService restaurantContractService,
            IAuditLogService auditLogService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _tableRepository = tableRepository;
            _restaurantContractService = restaurantContractService;
            _auditLogService = auditLogService;
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
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var tables = await _tableRepository
                .Query(x => x.RestaurantId == restaurantId)
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

        public async Task<TableResponse> UpdateAsync(int restaurantId, int tableId, UpdateTableRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId);

            var table = await GetTrackedTableAsync(restaurantId, tableId);
            await EnsureTableNumberIsUniqueAsync(restaurantId, request.TableNo, tableId);

            _mapper.Map(request, table);
            await _tableRepository.SaveChangesAsync();

            return MapTableResponse(table);
        }

        public async Task<TableResponse> ActivateAsync(int restaurantId, int tableId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId);

            var table = await GetTrackedTableAsync(restaurantId, tableId);
            table.IsActive = true;

            await _tableRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.TableActivated,
                new
                {
                    table.Id,
                    table.TableNo,
                    table.Name
                },
                AuditEntityTypes.Table,
                table.Id,
                table.Name);

            return MapTableResponse(table);
        }

        public async Task<TableResponse> DeactivateAsync(int restaurantId, int tableId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var table = await GetTrackedTableAsync(restaurantId, tableId);
            table.IsActive = false;

            await _tableRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.TableDeactivated,
                new
                {
                    table.Id,
                    table.TableNo,
                    table.Name
                },
                AuditEntityTypes.Table,
                table.Id,
                table.Name);

            return MapTableResponse(table);
        }

        public async Task<TableResponse> DeleteAsync(int restaurantId, int tableId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var table = await GetTrackedTableAsync(restaurantId, tableId);
            await _tableRepository.Delete(table);
            await _tableRepository.SaveChangesAsync();

            return MapTableResponse(table);
        }

        private async Task EnsureTableNumberIsUniqueAsync(int restaurantId, int tableNo, int? excludeTableId = null)
        {
            var tableExists = await _tableRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.TableNo == tableNo &&
                (!excludeTableId.HasValue || x.Id != excludeTableId.Value));

            if (tableExists)
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo });
        }

        private async Task<Domain.Entities.Table> GetTrackedTableAsync(int restaurantId, int tableId)
        {
            var table = await _tableRepository
                .QueryTracked(x => x.RestaurantId == restaurantId && x.Id == tableId)
                .FirstOrDefaultAsync();

            return table ?? throw new NotFoundException(ErrorCode.TableNotFound);
        }

        private static TableResponse MapTableResponse(Domain.Entities.Table table)
        {
            return new TableResponse
            {
                Id = table.Id,
                RestaurantId = table.RestaurantId,
                TableNo = table.TableNo,
                Name = table.Name,
                Capacity = table.Capacity,
                IsActive = table.IsActive,
                IsEmpty = table.IsEmpty
            };
        }
    }
}
