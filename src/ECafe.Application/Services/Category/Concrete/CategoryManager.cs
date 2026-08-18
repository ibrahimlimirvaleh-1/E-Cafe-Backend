using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Category;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Category.Abstract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ECafe.Shared.Extensions;
public class CategoryManager : BaseManager, ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantContractService _restaurantContractService;
    private readonly IAuditLogService _auditLogService;

    public CategoryManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        ICategoryRepository categoryRepository,
        IRestaurantRepository restaurantRepository,
        IRestaurantContractService restaurantContractService,
        IAuditLogService auditLogService)
        : base(httpContextAccessor, mapper, configuration)
    {
        _categoryRepository = categoryRepository;
        _restaurantRepository = restaurantRepository;
        _restaurantContractService = restaurantContractService;
        _auditLogService = auditLogService;
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

        await _auditLogService.RecordRestaurantActionAsync(
            request.RestaurantId,
            AuditActions.CategoryCreated,
            new
            {
                categoryId = category.Id,
                category.Name,
                category.Slug,
                category.SortOrder
            },
            AuditEntityTypes.Category,
            category.Id,
            category.Name);

        return category.Id;
    }

    public async Task<GetAllCategoryResponse> UpdateCategoryAsync(
        int restaurantId,
        int categoryId,
        UpdateCategoryRequest request)
    {
        if (request is null)
            throw new BusinessRuleException("Request is null!");

        await EnsureCategoryMutationContextAsync(restaurantId);

        var category = await GetTrackedCategoryAsync(restaurantId, categoryId);
        category.Name = request.Name.Trim();
        category.Slug = category.Name.GenerateSlug();
        category.SortOrder = await ResolveSortOrderAsync(restaurantId, request.SortOrder);
        category.IsActive = request.IsActive;

        await _categoryRepository.SaveChangesAsync();

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.CategoryUpdated,
            new
            {
                categoryId = category.Id,
                category.Name,
                category.Slug,
                category.SortOrder,
                category.IsActive
            },
            AuditEntityTypes.Category,
            category.Id,
            category.Name);

        return Mapper.Map<GetAllCategoryResponse>(category);
    }

    public async Task<GetAllCategoryResponse> DeactivateCategoryAsync(int restaurantId, int categoryId)
    {
        await EnsureCategoryMutationContextAsync(restaurantId);

        var category = await GetTrackedCategoryAsync(restaurantId, categoryId);
        category.IsActive = false;

        await _categoryRepository.SaveChangesAsync();

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.CategoryDeactivated,
            new
            {
                categoryId = category.Id,
                category.Name
            },
            AuditEntityTypes.Category,
            category.Id,
            category.Name);

        return Mapper.Map<GetAllCategoryResponse>(category);
    }

    public async Task<GetAllCategoryResponse> DeleteCategoryAsync(int restaurantId, int categoryId)
    {
        await EnsureCategoryMutationContextAsync(restaurantId);

        var category = await GetTrackedCategoryAsync(restaurantId, categoryId);
        category.IsActive = false;

        await _categoryRepository.Delete(category);
        await _categoryRepository.SaveChangesAsync();

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.CategoryDeleted,
            new
            {
                categoryId = category.Id,
                category.Name
            },
            AuditEntityTypes.Category,
            category.Id,
            category.Name);

        return Mapper.Map<GetAllCategoryResponse>(category);
    }

    public async Task<List<GetAllCategoryResponse>> GetCategoriesByRestaurantIdAsync(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        EnsureCurrentUserCanAccessRestaurant(restaurantId);

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

    private async Task EnsureCategoryMutationContextAsync(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        var restaurantExists = await _restaurantRepository
            .Query(x => x.Id == restaurantId && x.IsActive)
            .AnyAsync();

        if (!restaurantExists)
            throw new BusinessRuleException("Restaurant not found!");

        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        await _restaurantContractService.EnsureRestaurantHasActiveContractAsync(restaurantId);
    }

    private async Task<Category> GetTrackedCategoryAsync(int restaurantId, int categoryId)
    {
        if (categoryId <= 0)
            throw new BusinessRuleException("Invalid category ID!");

        var category = await _categoryRepository.GetByRestaurantAsync(restaurantId, categoryId);
        return category ?? throw new BusinessRuleException("Category not found!");
    }
}
