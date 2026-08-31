using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Item;
using ECafe.Application.Repository;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Item.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Item.Concrete
{
    public class ItemManager : BaseManager, IItemService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IFileRepository _fileRepository;
        private readonly IBaseRepository<Domain.Entities.Status> _statusRepository;
        private readonly IValidator<CreateItemRequest> _validator;
        private readonly IValidator<UpdateItemRequest> _updateValidator;
        private readonly IAuditLogService _auditLogService;
        private readonly IMinioService _minioService;

        public ItemManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           ICategoryRepository categoryRepository,
                           IRestaurantRepository restaurantRepository,
                           IItemRepository itemRepository,
                           IFileRepository fileRepository,
                           IBaseRepository<Domain.Entities.Status> statusRepository,
                           IValidator<CreateItemRequest> validator,
                           IValidator<UpdateItemRequest> updateValidator,
                           IAuditLogService auditLogService,
                           IMinioService minioService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _categoryRepository = categoryRepository;
            _restaurantRepository = restaurantRepository;
            _itemRepository = itemRepository;
            _fileRepository = fileRepository;
            _statusRepository = statusRepository;
            _validator = validator;
            _updateValidator = updateValidator;
            _auditLogService = auditLogService;
            _minioService = minioService;
        }

        public async Task<int> CreateAsync(CreateItemRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            await _validator.ValidateAndThrowAsync(request);

            var itemName = request.Name.Trim();

            await EnsureRestaurantExistsAsync(request.RestaurantId);
            EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
            var category = await EnsureCategoryBelongsToRestaurantAsync(request.CategoryId, request.RestaurantId);
            await EnsureItemNameIsUniqueAsync(request.RestaurantId, request.CategoryId, itemName);
            var status = await _statusRepository.GetByIdAsync(request.StatusId);

            var item = Mapper.Map<Domain.Entities.Item>(request);
            item.File = await GetAttachableFileAsync(request.FileId);

            await _itemRepository.Add(item);
            await _itemRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                request.RestaurantId,
                AuditActions.ItemCreated,
                new
                {
                    itemId = item.Id,
                    item.Name,
                    item.CategoryId,
                    CategoryName = category.Name,
                    item.BasePrice,
                    item.StatusId,
                    StatusName = status?.Name,
                    item.FileId
                },
                AuditEntityTypes.Item,
                item.Id,
                item.Name);

            return item.Id;
        }

        public async Task<int> UpdateAsync(int restaurantId, int itemId, UpdateItemRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (itemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidItemId);

            await _updateValidator.ValidateAndThrowAsync(request);

            await EnsureRestaurantExistsAsync(restaurantId);
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var item = await GetTrackedItemAsync(restaurantId, itemId);
            var category = await EnsureCategoryBelongsToRestaurantAsync(request.CategoryId, restaurantId);
            var status = await _statusRepository.GetByIdAsync(request.StatusId);
            var itemName = request.Name.Trim();

            await EnsureItemNameIsUniqueAsync(restaurantId, request.CategoryId, itemName, itemId);

            item.CategoryId = request.CategoryId;
            item.StatusId = request.StatusId;
            item.Name = itemName;
            item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            item.BasePrice = request.BasePrice;
            item.UnavailableReason = string.IsNullOrWhiteSpace(request.UnavailableReason) ? null : request.UnavailableReason.Trim();
            item.SalesCount = request.SalesCount;
            item.IsAvailable = request.StatusId != GetOutOfStockStatusId();

            if (request.FileId.HasValue && request.FileId.Value != item.FileId)
            {
                item.File = await GetAttachableFileAsync(request.FileId);
                item.FileId = request.FileId.Value;
            }

            await _itemRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ItemUpdated,
                new
                {
                    itemId = item.Id,
                    item.Name,
                    item.CategoryId,
                    CategoryName = category.Name,
                    item.BasePrice,
                    item.StatusId,
                    StatusName = status?.Name,
                    item.FileId
                },
                AuditEntityTypes.Item,
                item.Id,
                item.Name);

            return item.Id;
        }

        public async Task<int> DeactivateAsync(int restaurantId, int itemId)
        {
            var item = await GetItemForMutationAsync(restaurantId, itemId);

            item.IsActive = false;
            item.IsAvailable = false;
            item.StatusId = GetOutOfStockStatusId();

            await _itemRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ItemDeactivated,
                new
                {
                    itemId = item.Id,
                    item.Name
                },
                AuditEntityTypes.Item,
                item.Id,
                item.Name);

            return item.Id;
        }

        public async Task<int> DeleteAsync(int restaurantId, int itemId)
        {
            var item = await GetItemForMutationAsync(restaurantId, itemId);

            item.IsActive = false;
            item.IsAvailable = false;

            await _itemRepository.Delete(item);
            await _itemRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ItemDeleted,
                new
                {
                    itemId = item.Id,
                    item.Name
                },
                AuditEntityTypes.Item,
                item.Id,
                item.Name);

            return item.Id;
        }

        public async Task<GetAllItemResponse> GetAllAsync(PaginationFilter filter, int restaurantId, int categoryId, int statusId)
        {
            var normalizedFilter = NormalizeFilter(filter);
            var targetRestaurantId = await ResolveTargetRestaurantIdAsync(restaurantId, categoryId);

            var query = BuildItemQuery(targetRestaurantId, categoryId, statusId);
            var paginatedItems = await PaginatedList<Domain.Entities.Item>.CreateAsync(
                query,
                normalizedFilter.PageNumber,
                normalizedFilter.PageSize);

            var itemDtos = Mapper.Map<List<ItemDto>>(paginatedItems.Items);
            await PopulateItemFileUrlsAsync(itemDtos, paginatedItems.Items);

            var responseItems = new PaginatedList<ItemDto>(
                itemDtos,
                paginatedItems.TotalCount,
                paginatedItems.PageIndex,
                normalizedFilter.PageSize);

            return Mapper.Map<GetAllItemResponse>(new GetAllItemResponseMapData
            {
                Items = responseItems
            });
        }

        private async Task<int> ResolveTargetRestaurantIdAsync(int restaurantId, int categoryId)
        {
            var targetRestaurantId = restaurantId;

            if (categoryId > 0)
            {
                var category = await GetCategoryAsync(categoryId);

                if (targetRestaurantId > 0 && category.RestaurantId != targetRestaurantId)
                    throw new BusinessRuleException(ErrorCode.CategoryDoesNotBelongToRestaurant);

                targetRestaurantId = category.RestaurantId;
            }

            if (!IsCurrentUserSuperAdmin())
            {
                var currentRestaurantId = GetRequiredCurrentRestaurantId();
                if (targetRestaurantId > 0 && targetRestaurantId != currentRestaurantId)
                    EnsureCurrentUserCanAccessRestaurant(targetRestaurantId);

                targetRestaurantId = currentRestaurantId;
            }

            return targetRestaurantId;
        }

        private IQueryable<Domain.Entities.Item> BuildItemQuery(int restaurantId, int categoryId, int statusId)
        {
            var query = _itemRepository.Query()
                .Include(x => x.Category)
                .Include(x => x.Status)
                .Include(x => x.File)
                .AsQueryable();

            if (restaurantId > 0)
                query = query.Where(x => x.RestaurantId == restaurantId);

            if (categoryId > 0)
                query = query.Where(x => x.CategoryId == categoryId);

            if (statusId > 0)
                query = query.Where(x => x.StatusId == statusId);

            return query.OrderBy(x => x.Name);
        }

        private static PaginationFilter NormalizeFilter(PaginationFilter? filter)
            => PaginationFilterNormalizer.Normalize(filter);

        private async Task PopulateItemFileUrlsAsync(
            IReadOnlyList<ItemDto> itemDtos,
            IReadOnlyList<Domain.Entities.Item> items)
        {
            var fileTokenByItemId = items.ToDictionary(item => item.Id, item => item.File?.Token);

            await Task.WhenAll(itemDtos.Select(async itemDto =>
            {
                if (!fileTokenByItemId.TryGetValue(itemDto.Id, out var token) || string.IsNullOrWhiteSpace(token))
                    return;

                itemDto.FileUrl = await _minioService.GenerateFileUrl(token);
            }));
        }

        private async Task EnsureRestaurantExistsAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);
            if (restaurant is null)
                throw new BusinessRuleException(ErrorCode.RestaurantNotFound);
        }

        private async Task<Domain.Entities.Category> EnsureCategoryBelongsToRestaurantAsync(int categoryId, int restaurantId)
        {
            var category = await GetCategoryAsync(categoryId);

            if (category.RestaurantId != restaurantId)
                throw new BusinessRuleException(ErrorCode.CategoryDoesNotBelongToRestaurant);

            return category;
        }

        private async Task<Domain.Entities.Category> GetCategoryAsync(int categoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category is null)
                throw new BusinessRuleException(ErrorCode.CategoryNotFound);

            return category;
        }

        private async Task<Domain.Entities.Item> GetItemForMutationAsync(int restaurantId, int itemId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (itemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidItemId);

            await EnsureRestaurantExistsAsync(restaurantId);
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            return await GetTrackedItemAsync(restaurantId, itemId);
        }

        private async Task<Domain.Entities.Item> GetTrackedItemAsync(int restaurantId, int itemId)
        {
            var item = await _itemRepository
                .QueryTracked(x => x.RestaurantId == restaurantId && x.Id == itemId)
                .FirstOrDefaultAsync();

            return item ?? throw new BusinessRuleException(ErrorCode.ItemNotFound);
        }

        private async Task EnsureItemNameIsUniqueAsync(int restaurantId, int categoryId, string itemName, int? excludeItemId = null)
        {
            var existItem = await _itemRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.CategoryId == categoryId &&
                (!excludeItemId.HasValue || x.Id != excludeItemId.Value) &&
                x.Name == itemName);

            if (existItem)
                throw new BusinessRuleException(ErrorCode.ItemAlreadyExistsInCategory);
        }

        private static int GetOutOfStockStatusId()
            => ((int)StatusType.ItemStatus * 1000) + (int)ItemStatus.OutOfStock;

        private async Task<Domain.Entities.File?> GetAttachableFileAsync(int? fileId)
        {
            if (!fileId.HasValue)
                return null;

            var file = await _fileRepository.GetAttachableByIdAsync(fileId.Value);
            if (file is null)
                throw new BusinessRuleException(ErrorCode.FileNotFoundOrAlreadyAttached);

            file.FileTypeId = (int)FileTypeCode.MenuItemImage;

            return file;
        }
    }
}
