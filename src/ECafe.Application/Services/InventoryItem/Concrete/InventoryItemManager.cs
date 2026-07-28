using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Unit;
using ECafe.Application.Services.InventoryItem.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.InventoryItem.Concrete
{
    public class InventoryItemManager : BaseManager, IInventoryItemService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IInventoryItemRepository _inventoryItemRepository;
        private readonly IUnitRepository _unitRepository;

        public InventoryItemManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantRepository restaurantRepository,
            IInventoryItemRepository inventoryItemRepository,
            IUnitRepository unitRepository)
            : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _inventoryItemRepository = inventoryItemRepository;
            _unitRepository = unitRepository;
        }

        public async Task<InventoryItemDto> CreateAsync(int restaurantId, CreateInventoryItemRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            var itemName = request.Name.Trim();

            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var unit = await _unitRepository.GetByIdAsync(request.UnitId);
            if (unit is null)
                throw new NotFoundException(ErrorCode.UnitNotFound);

            var inventoryItemExists = await _inventoryItemRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.Name == itemName);

            if (inventoryItemExists)
                throw new BusinessRuleException(ErrorCode.InventoryItemAlreadyExists, new { name = itemName });

            var inventoryItem = Mapper.Map<Domain.Entities.InventoryItem>(request);
            inventoryItem.RestaurantId = restaurantId;

            await _inventoryItemRepository.Add(inventoryItem);
            await _inventoryItemRepository.SaveChangesAsync();

            inventoryItem.Unit = unit;

            return Mapper.Map<InventoryItemDto>(inventoryItem);
        }

        public async Task<PaginatedList<InventoryItemDto>> ListAsync(
            PaginationFilter paginationFilter,
            string? search,
            bool onlyLowStock,
            int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var normalizedFilter = PaginationFilterNormalizer.Normalize(paginationFilter);
            var normalizedSearch = NormalizeSearch(search);

            var query = _inventoryItemRepository.Query(x => x.RestaurantId == restaurantId)
                .Include(x => x.Unit)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
                query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch));

            if (onlyLowStock)
                query = query.Where(x => x.QuantityOnHand <= x.LowStockThreshold);

            query = query.OrderBy(x => x.Name);

            var paginatedItems = await PaginatedList<Domain.Entities.InventoryItem>.CreateAsync(
                query,
                normalizedFilter.PageNumber,
                normalizedFilter.PageSize);

            var itemDtos = Mapper.Map<List<InventoryItemDto>>(paginatedItems.Items);

            return new PaginatedList<InventoryItemDto>(
                itemDtos,
                paginatedItems.TotalCount,
                paginatedItems.PageIndex,
                normalizedFilter.PageSize);
        }

        public async Task<InventoryItemDto> UpdateAsync(
            UpdateInventoryItemRequest request,
            int restaurantId,
            int inventoryItemId)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (inventoryItemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidInventoryItemId);

            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var inventoryItem = await _inventoryItemRepository.GetInventoryByRestaurantIdAsync(inventoryItemId, restaurantId);
            if (inventoryItem is null)
                throw new NotFoundException(ErrorCode.InventoryItemNotFound);

            var itemName = request.Name.Trim();

            var unit = await _unitRepository.GetByIdAsync(request.UnitId);
            if (unit is null)
                throw new NotFoundException(ErrorCode.UnitNotFound);

            var inventoryItemExists = await _inventoryItemRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.Id != inventoryItemId &&
                x.Name == itemName);

            if (inventoryItemExists)
                throw new BusinessRuleException(ErrorCode.InventoryItemAlreadyExists, new { name = itemName });

            Mapper.Map(request, inventoryItem);

            await _inventoryItemRepository.SaveChangesAsync();

            inventoryItem.Unit = unit;

            return Mapper.Map<InventoryItemDto>(inventoryItem);
        }

        private static string? NormalizeSearch(string? search)
            => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
    }
}
