using AutoMapper;
using ECafe.Application.DTOs.Category;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services;
using ECafe.Application.Services.Category.Abstract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ECafe.Shared.Extensions;
public class CategoryManager : BaseManager, ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantContractService _restaurantContractService;

    public CategoryManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        ICategoryRepository categoryRepository,
        IRestaurantRepository restaurantRepository,
        IRestaurantContractService restaurantContractService)
        : base(httpContextAccessor, mapper, configuration)
    {
        _categoryRepository = categoryRepository;
        _restaurantRepository = restaurantRepository;
        _restaurantContractService = restaurantContractService;
    }

    public async Task<int> CreateCategoryAsync(CreateCategoryRequest request)
    {
        if (request is null)
            throw new BusinessRuleException("Request is null!");

        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId);
        if (restaurant is null)
            throw new BusinessRuleException("Restaurant not found!");

        EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
        await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(request.RestaurantId);

        var category = Mapper.Map<Category>(request);
        category.Slug = request.Name.GenerateSlug();
        category.SortOrder = await ResolveSortOrderAsync(request.RestaurantId, request.SortOrder);
        category.IsActive = true;

        await _categoryRepository.Add(category);
        await _categoryRepository.SaveChangesAsync();

        return category.Id;
    }

    public async Task<List<GetAllCategoryResponse>> GetCategoriesByRestaurantIdAsync(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        var categories = await _categoryRepository.GetCategoriesByRestaurantIdAsync(restaurantId);

        if (!categories.Any())
            throw new BusinessRuleException("Category is empty");

        return Mapper.Map<List<GetAllCategoryResponse>>(categories);
    }

    private async Task<int> ResolveSortOrderAsync(int restaurantId, int? requestedSortOrder)
    {
        var sortOrder = requestedSortOrder.GetValueOrDefault();
        if (sortOrder > 0)
            return sortOrder;

        var maxSortOrder = await _categoryRepository.GetMaxSortOrderByRestaurantIdAsync(restaurantId);
        return maxSortOrder + 1;
    }
}
