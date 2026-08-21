using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Services;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Domain.Entities;
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

        public async Task<List<TableResponse>> CopyTableAsync(int restaurantId, int tableId, CopyTableRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId);

            var table = await GetTrackedTableAsync(restaurantId, tableId);
            var copyInputs = NormalizeCopyInputs(request);
            var tableNumbers = await ResolveCopyTableNumbersAsync(restaurantId, copyInputs);

            var newTables = copyInputs
                .Select((copyInput, index) => new Domain.Entities.Table
                {
                    RestaurantId = restaurantId,
                    Capacity = table.Capacity,
                    TableNo = tableNumbers[index],
                    Name = BuildCopiedTableName(copyInput.Name, tableNumbers[index]),
                    IsActive = true,
                    IsEmpty = true
                })
                .ToList();

            await EnsureTableNamesAreUniqueAsync(restaurantId, newTables.Select(x => x.Name).ToList());

            try
            {
                foreach (var newTable in newTables)
                    await _tableRepository.Add(newTable);

                await _tableRepository.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsTableNumberUniqueViolation(ex))
            {
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo = request.TableNo ?? tableNumbers.First() });
            }

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.TableCopied,
                new
                {
                    SourceTableId = table.Id,
                    SourceTableNo = table.TableNo,
                    CopyCount = newTables.Count,
                    NewTables = newTables.Select(x => new
                    {
                        x.Id,
                        x.TableNo,
                        x.Name,
                        x.Capacity
                    })
                },
                AuditEntityTypes.Table,
                table.Id,
                table.Name);

            return newTables.Select(MapTableResponse).ToList();
        }

        private static List<CopyTableInput> NormalizeCopyInputs(CopyTableRequest request)
        {
            if (request.Copies?.Count > 0)
                return request.Copies
                    .Select(x => new CopyTableInput(x.TableNo, x.Name?.Trim()))
                    .ToList();

            var copyCount = Math.Max(request.CopyCount, 1);
            return Enumerable
                .Range(0, copyCount)
                .Select(index => new CopyTableInput(
                    request.TableNo.HasValue ? request.TableNo.Value + index : null,
                    request.Name?.Trim()))
                .ToList();
        }

        private async Task<List<int>> ResolveCopyTableNumbersAsync(int restaurantId, IReadOnlyList<CopyTableInput> copyInputs)
        {
            var requestedNumbers = copyInputs
                .Where(x => x.TableNo.HasValue)
                .Select(x => x.TableNo!.Value)
                .ToList();

            if (requestedNumbers.Count > 0)
                await EnsureTableNumbersAreUniqueAsync(restaurantId, requestedNumbers);

            var generatedNumbers = await GetNextAvailableTableNumbersAsync(restaurantId, copyInputs.Count(x => !x.TableNo.HasValue), requestedNumbers);
            var generatedNumberIndex = 0;

            return copyInputs
                .Select(copyInput => copyInput.TableNo ?? generatedNumbers[generatedNumberIndex++])
                .ToList();
        }

        private async Task<List<int>> GetNextAvailableTableNumbersAsync(int restaurantId, int count, IReadOnlyCollection<int> reservedNumbers)
        {
            if (count == 0)
                return new List<int>();

            var usedNumbers = await _tableRepository
                .Query(x => x.RestaurantId == restaurantId)
                .IgnoreQueryFilters()
                .Select(x => x.TableNo)
                .ToListAsync();

            var unavailableNumbers = usedNumbers
                .Concat(reservedNumbers)
                .ToHashSet();

            var nextNumber = usedNumbers.Count == 0 ? 1 : usedNumbers.Max() + 1;
            var generatedNumbers = new List<int>();

            while (generatedNumbers.Count < count)
            {
                if (unavailableNumbers.Add(nextNumber))
                    generatedNumbers.Add(nextNumber);

                nextNumber++;
            }

            return generatedNumbers;
        }

        private static string BuildCopiedTableName(string? requestedName, int tableNo)
        {
            var name = string.IsNullOrWhiteSpace(requestedName)
                ? $"Masa {tableNo}"
                : requestedName;

            return name.Length <= 100 ? name : name[..100];
        }

        private async Task EnsureTableNumberIsUniqueAsync(int restaurantId, int tableNo, int? excludeTableId = null)
        {
            var tableExists = await _tableRepository
                .Query()
                .IgnoreQueryFilters()
                .AnyAsync(x =>
                    x.RestaurantId == restaurantId &&
                    x.TableNo == tableNo &&
                    (!excludeTableId.HasValue || x.Id != excludeTableId.Value));

            if (tableExists)
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo });
        }

        private async Task EnsureTableNumbersAreUniqueAsync(int restaurantId, IReadOnlyCollection<int> tableNumbers)
        {
            var distinctTableNumbers = tableNumbers.Distinct().ToList();
            if (distinctTableNumbers.Count != tableNumbers.Count)
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo = tableNumbers.First() });

            var existingTableNo = await _tableRepository
                .Query()
                .IgnoreQueryFilters()
                .Where(x => x.RestaurantId == restaurantId && distinctTableNumbers.Contains(x.TableNo))
                .Select(x => x.TableNo)
                .FirstOrDefaultAsync();

            if (existingTableNo > 0)
                throw new BusinessRuleException(ErrorCode.TableAlreadyExists, new { tableNo = existingTableNo });
        }

        private async Task EnsureTableNamesAreUniqueAsync(int restaurantId, IReadOnlyCollection<string?> tableNames)
        {
            var normalizedNames = tableNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToLower())
                .ToList();

            if (normalizedNames.Count != normalizedNames.Distinct().Count())
                throw new BusinessRuleException("Masa adları təkrar olmamalıdır.");

            var existingName = await _tableRepository
                .Query()
                .IgnoreQueryFilters()
                .Where(x => x.RestaurantId == restaurantId && x.Name != null && normalizedNames.Contains(x.Name!.ToLower()))
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(existingName))
                throw new BusinessRuleException($"'{existingName}' adlı masa artıq mövcuddur.");
        }

        private sealed record CopyTableInput(int? TableNo, string? Name);

        private static bool IsTableNumberUniqueViolation(DbUpdateException exception)
        {
            var innerException = exception.InnerException;
            if (innerException is null)
                return false;

            var exceptionType = innerException.GetType();
            var sqlState = exceptionType.GetProperty("SqlState")?.GetValue(innerException)?.ToString();
            var constraintName = exceptionType.GetProperty("ConstraintName")?.GetValue(innerException)?.ToString();

            return sqlState == "23505" &&
                   constraintName == "tables_restaurant_id_table_no_key";
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
