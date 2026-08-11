using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Item;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Item.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
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
        private readonly IValidator<CreateItemRequest> _validator;
        private readonly IRestaurantContractService _restaurantContractService;
        private readonly IAuditLogService _auditLogService;
        private readonly IMinioService _minioService;

        public ItemManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           ICategoryRepository categoryRepository,
                           IRestaurantRepository restaurantRepository,
                           IItemRepository itemRepository,
                           IFileRepository fileRepository,
                           IValidator<CreateItemRequest> validator,
                           IRestaurantContractService restaurantContractService,
                           IAuditLogService auditLogService,
                           IMinioService minioService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _categoryRepository = categoryRepository;
            _restaurantRepository = restaurantRepository;
            _itemRepository = itemRepository;
            _fileRepository = fileRepository;
            _validator = validator;
            _restaurantContractService = restaurantContractService;
            _auditLogService = auditLogService;
            _minioService = minioService;
        }

        public async Task<int> CreateAsync(CreateItemRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request cannot be null.");

            await _validator.ValidateAndThrowAsync(request);

            var itemName = request.Name.Trim();

            await EnsureRestaurantExistsAsync(request.RestaurantId);
            EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
            await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(request.RestaurantId);
            await EnsureCategoryBelongsToRestaurantAsync(request.CategoryId, request.RestaurantId);
            await EnsureItemNameIsUniqueAsync(request.RestaurantId, request.CategoryId, itemName);

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
                    item.BasePrice,
                    item.StatusId,
                    item.FileId
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
                    throw new BusinessRuleException("Category does not belong to the selected restaurant.");

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
                throw new BusinessRuleException("Restaurant not found!");
        }

        private async Task EnsureCategoryBelongsToRestaurantAsync(int categoryId, int restaurantId)
        {
            var category = await GetCategoryAsync(categoryId);

            if (category.RestaurantId != restaurantId)
                throw new BusinessRuleException("Category does not belong to the selected restaurant.");
        }

        private async Task<Domain.Entities.Category> GetCategoryAsync(int categoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category is null)
                throw new BusinessRuleException("Category not found!");

            return category;
        }

        private async Task EnsureItemNameIsUniqueAsync(int restaurantId, int categoryId, string itemName)
        {
            var existItem = await _itemRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.CategoryId == categoryId &&
                x.Name == itemName);

            if (existItem)
                throw new BusinessRuleException("Item with the same name already exists in this category.");
        }

        private async Task<Domain.Entities.File?> GetAttachableFileAsync(int? fileId)
        {
            if (!fileId.HasValue)
                return null;

            var file = await _fileRepository.GetAttachableByIdAsync(fileId.Value);
            if (file is null)
                throw new BusinessRuleException("File not found or already attached.");

            file.FileTypeId = (int)FileTypeCode.MenuItemImage;

            return file;
        }
    }
}
