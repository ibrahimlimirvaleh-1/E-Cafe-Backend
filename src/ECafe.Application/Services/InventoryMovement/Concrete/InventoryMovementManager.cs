using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Repositories.InventoryMovementType;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Unit;
using ECafe.Application.Repository;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace ECafe.Application.Services.InventoryMovement.Concrete
{
    public class InventoryMovementManager : BaseManager, IInventoryMovementService
    {
        private static readonly InventoryMovementTypeCode[] IncreaseMovementTypes =
        [
            InventoryMovementTypeCode.Purchase,
            InventoryMovementTypeCode.ManualIncrease,
            InventoryMovementTypeCode.StockReturn,
            InventoryMovementTypeCode.Correction
        ];

        private static readonly InventoryMovementTypeCode[] DecreaseMovementTypes =
        [
            InventoryMovementTypeCode.ManualDecrease,
            InventoryMovementTypeCode.OrderConsumption,
            InventoryMovementTypeCode.Waste
        ];

        private readonly IInventoryMovementRepository _inventoryMovementRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IInventoryItemRepository _inventoryItemRepository;
        private readonly IInventoryMovementTypeRepository _inventoryMovementTypeRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly IInventoryLowStockNotifier _lowStock;
        private readonly IApplicationDbTransactionFactory _transactions;

        public InventoryMovementManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IInventoryMovementRepository inventoryMovementRepository,
            IRestaurantRepository restaurantRepository,
            IInventoryItemRepository inventoryItemRepository,
            IInventoryMovementTypeRepository inventoryMovementTypeRepository,
            IUnitRepository unitRepository,
            IInventoryLowStockNotifier lowStock,
            IApplicationDbTransactionFactory transactions)
            : base(httpContextAccessor, mapper, configuration)
        {
            _inventoryMovementRepository = inventoryMovementRepository;
            _restaurantRepository = restaurantRepository;
            _inventoryItemRepository = inventoryItemRepository;
            _inventoryMovementTypeRepository = inventoryMovementTypeRepository;
            _unitRepository = unitRepository;
            _lowStock = lowStock;
            _transactions = transactions;
        }

        /// <summary>Əl ilə stok hərəkətini mətbəxlə eyni sətir kilidi altında saxlayır; qalıq, hərəkət və minimum bildirişi birlikdə commit olunur.</summary>
        public async Task<InventoryMovementResponse> CreateAsync(
            int inventoryItemId,
            int restaurantId,
            CreateInventoryMovementRequest request)
        {
            ValidateCreateRequest(inventoryItemId, restaurantId, request);
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            await EnsureRestaurantExistsAsync(restaurantId);
            await using var transaction = await _transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _inventoryItemRepository.LockForStockChangeAsync(restaurantId, [inventoryItemId]);
            var inventoryItem = await GetInventoryItemAsync(inventoryItemId, restaurantId);
            var requestUnit = await GetUnitAsync(request.UnitId);
            var stockUnit = await GetUnitAsync(inventoryItem.UnitId);
            var movementType = await GetMovementTypeAsync(request.MovementTypeId);

            var movementTypeCode = ResolveMovementTypeCode(movementType.Id);
            var normalizedQuantity = InventoryStockPolicy.ConvertQuantity(request.Quantity, requestUnit, stockUnit);
            var quantityChange = ResolveQuantityChange(normalizedQuantity, movementTypeCode);
            var quantityBeforeMovement = inventoryItem.QuantityOnHand;
            InventoryStockPolicy.ApplyChange(inventoryItem, quantityChange);
            var quantityAfterMovement = inventoryItem.QuantityOnHand;

            var movement = CreateMovement(
                request,
                restaurantId,
                inventoryItemId,
                stockUnit.Id,
                quantityChange);

            await _inventoryMovementRepository.Add(movement);
            await _lowStock.NotifyAsync(inventoryItem, stockUnit, quantityBeforeMovement);
            await _inventoryMovementRepository.SaveChangesAsync();
            await transaction.CommitAsync();
            return BuildResponse(movement, stockUnit, movementType, quantityAfterMovement);
        }



        public async Task<PaginatedList<InventoryMovementHistoryResponse>> HistoryAsync(
            int inventoryItemId,
            int restaurantId,
            PaginationFilter paginationFilter)
        {
            var normalizedFilter = PaginationFilterNormalizer.Normalize(paginationFilter);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (inventoryItemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidInventoryItemId);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);

            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);

            var inventoryItemExists = await _inventoryItemRepository
                .Query(x => x.RestaurantId == restaurantId && x.Id == inventoryItemId)
                .AnyAsync();

            if (!inventoryItemExists)
                throw new NotFoundException(ErrorCode.InventoryItemNotFound);

            var movements = _inventoryMovementRepository
                .Query(x => x.RestaurantId == restaurantId && x.InventoryItemId == inventoryItemId)
                .OrderByDescending(x => x.CreatedAt)
                .ProjectTo<InventoryMovementHistoryResponse>(Mapper.ConfigurationProvider);

            return await PaginatedList<InventoryMovementHistoryResponse>.CreateAsync(
                movements,
                normalizedFilter.PageNumber,
                normalizedFilter.PageSize);
        }


        #region Create helper method
        private static void ValidateCreateRequest(
           int inventoryItemId,
           int restaurantId,
           CreateInventoryMovementRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (inventoryItemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidInventoryItemId);

            if (request.Quantity <= 0)
                throw new BusinessRuleException(ErrorCode.InventoryMovementQuantityMustBeGreaterThanZero);
        }

        private async Task EnsureRestaurantExistsAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);
        }

        private async Task<Domain.Entities.InventoryItem> GetInventoryItemAsync(
            int inventoryItemId,
            int restaurantId)
        {
            var inventoryItem = await _inventoryItemRepository.GetInventoryByRestaurantIdAsync(inventoryItemId, restaurantId);
            return inventoryItem ?? throw new NotFoundException(ErrorCode.InventoryItemNotFound);
        }

        private async Task<Unit> GetUnitAsync(int unitId)
        {
            var unit = await _unitRepository.GetByIdAsync(unitId);
            return unit ?? throw new NotFoundException(ErrorCode.UnitNotFound);
        }

        private async Task<InventoryMovementType> GetMovementTypeAsync(int movementTypeId)
        {
            var movementType = await _inventoryMovementTypeRepository.GetByIdAsync(movementTypeId);
            return movementType ?? throw new BusinessRuleException(ErrorCode.InventoryMovementTypeNotFound);
        }

        private Domain.Entities.InventoryMovement CreateMovement(
            CreateInventoryMovementRequest request,
            int restaurantId,
            int inventoryItemId,
            int stockUnitId,
            decimal quantityChange)
        {
            var movement = Mapper.Map<Domain.Entities.InventoryMovement>(request);
            movement.RestaurantId = restaurantId;
            movement.InventoryItemId = inventoryItemId;
            movement.UnitId = stockUnitId;
            movement.QuantityChange = quantityChange;

            return movement;
        }

        private InventoryMovementResponse BuildResponse(
            Domain.Entities.InventoryMovement movement,
            Unit stockUnit,
            InventoryMovementType movementType,
            decimal quantityAfterMovement)
        {
            movement.Unit = stockUnit;
            movement.MovementType = movementType;

            var response = Mapper.Map<InventoryMovementResponse>(movement);
            response.QuantityAfterMovement = quantityAfterMovement;

            return response;
        }

        private static InventoryMovementTypeCode ResolveMovementTypeCode(int movementTypeId)
        {
            if (!Enum.IsDefined(typeof(InventoryMovementTypeCode), movementTypeId))
                throw new BusinessRuleException(ErrorCode.InvalidInventoryMovementType);

            return (InventoryMovementTypeCode)movementTypeId;
        }

        private static decimal ResolveQuantityChange(
            decimal quantity,
            InventoryMovementTypeCode movementType)
        {
            if (IncreaseMovementTypes.Contains(movementType))
                return quantity;

            if (DecreaseMovementTypes.Contains(movementType))
                return -quantity;

            throw new BusinessRuleException(ErrorCode.InvalidInventoryMovementType);
        }


        #endregion


    }
}
