using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.Item;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.Item.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Item.Concrete
{
    public class ItemManager : BaseManager, IItemService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IMinioService _minioService;
        private readonly IValidator<CreateItemRequest> _validator;
        private readonly IRestaurantContractService _restaurantContractService;

        public ItemManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           ICategoryRepository categoryRepository,
                           IRestaurantRepository restaurantRepository,
                           IItemRepository itemRepository,
                           IMinioService minioService,
                           IValidator<CreateItemRequest> validator,
                           IRestaurantContractService restaurantContractService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _categoryRepository = categoryRepository;
            _restaurantRepository = restaurantRepository;
            _itemRepository = itemRepository;
            _minioService = minioService;
            _validator = validator;
            _restaurantContractService = restaurantContractService;
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
            item.File = await CreateFileIfExistsAsync(request.File);

            await _itemRepository.Add(item);
            await _itemRepository.SaveChangesAsync();

            return item.Id;
        }

        public async Task<GetAllItemResponse> GetAllAsync(PaginationFilter filter, int categoryId, int statusId)
        {
            filter ??= new PaginationFilter();

            if (filter.PageNumber <= 0)
                filter.PageNumber = 1;

            if (filter.PageSize <= 0)
                filter.PageSize = 5;

            var query = _itemRepository.Query();

            if (categoryId > 0)
                query = query.Where(x => x.CategoryId == categoryId);

            if (statusId > 0)
                query = query.Where(x => x.StatusId == statusId);

            var items = query
                .OrderBy(x => x.Name)
                .ProjectTo<ItemDto>(Mapper.ConfigurationProvider);

            var paginatedItems = await PaginatedList<ItemDto>.CreateAsync(items, filter.PageNumber, filter.PageSize);
            return Mapper.Map<GetAllItemResponse>(new GetAllItemResponseMapData
            {
                Items = paginatedItems
            });
        }

        private async Task EnsureRestaurantExistsAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");
        }

        private async Task EnsureCategoryBelongsToRestaurantAsync(int categoryId, int restaurantId)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category is null)
                throw new BusinessRuleException("Category not found!");

            if (category.RestaurantId != restaurantId)
                throw new BusinessRuleException("Category does not belong to the selected restaurant.");
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

        private async Task<Domain.Entities.File?> CreateFileIfExistsAsync(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                return null;

            var token = await _minioService.UploadFileAsync(new UploadFileDto(file));

            return Mapper.Map<Domain.Entities.File>(new FileMapData
            {
                Token = token,
                FileName = file.FileName,
                Size = file.Length
            });
        }
    }
}
