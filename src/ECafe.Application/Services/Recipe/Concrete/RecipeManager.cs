using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Recipe;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Unit;
using ECafe.Application.Services.Recipe.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Recipe.Concrete
{
    public class RecipeManager : BaseManager, IRecipeService
    {
        private readonly IRecipeRepository _recipeRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IInventoryItemRepository _inventoryItemRepository;
        private readonly IUnitRepository _unitRepository;

        public RecipeManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRecipeRepository recipeRepository,
            IRestaurantRepository restaurantRepository,
            IItemRepository itemRepository,
            IInventoryItemRepository inventoryItemRepository,
            IUnitRepository unitRepository)
            : base(httpContextAccessor, mapper, configuration)
        {
            _recipeRepository = recipeRepository;
            _restaurantRepository = restaurantRepository;
            _itemRepository = itemRepository;
            _inventoryItemRepository = inventoryItemRepository;
            _unitRepository = unitRepository;
        }

        public async Task<List<RecipeDto>> GetByItemAsync(int restaurantId, int itemId)
        {
            await EnsureRecipeContextAsync(restaurantId, itemId);

            var recipes = await _recipeRepository.GetByItemAsync(restaurantId, itemId);
            return Mapper.Map<List<RecipeDto>>(recipes);
        }

        public async Task<RecipeDto> CreateAsync(int restaurantId, int itemId, CreateRecipeRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            var item = await EnsureRecipeContextAsync(restaurantId, itemId);
            var inventoryItem = await GetActiveInventoryItemAsync(restaurantId, request.InventoryItemId);
            var recipeUnit = await GetUnitAsync(request.UnitId);
            var stockUnit = await GetUnitAsync(inventoryItem.UnitId);

            EnsureUnitConversionAllowed(recipeUnit, stockUnit);

            var exists = await _recipeRepository.ExistsAsync(restaurantId, itemId, request.InventoryItemId);
            if (exists)
                throw new BusinessRuleException("Recipe ingredient already exists for this item.");

            var recipe = Mapper.Map<Domain.Entities.Recipe>(request);
            recipe.RestaurantId = restaurantId;
            recipe.ItemId = itemId;

            await _recipeRepository.Add(recipe);
            await _recipeRepository.SaveChangesAsync();

            recipe.Item = item;
            recipe.InventoryItem = inventoryItem;
            recipe.Unit = recipeUnit;

            return Mapper.Map<RecipeDto>(recipe);
        }

        public async Task<RecipeDto> UpdateAsync(
            int restaurantId,
            int itemId,
            int recipeId,
            UpdateRecipeRequest request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            var item = await EnsureRecipeContextAsync(restaurantId, itemId);
            var recipe = await GetRecipeForMutationAsync(restaurantId, itemId, recipeId);
            var inventoryItem = await GetActiveInventoryItemAsync(restaurantId, request.InventoryItemId);
            var recipeUnit = await GetUnitAsync(request.UnitId);
            var stockUnit = await GetUnitAsync(inventoryItem.UnitId);

            EnsureUnitConversionAllowed(recipeUnit, stockUnit);

            var exists = await _recipeRepository.ExistsAsync(restaurantId, itemId, request.InventoryItemId, recipeId);
            if (exists)
                throw new BusinessRuleException("Recipe ingredient already exists for this item.");

            Mapper.Map(request, recipe);
            await _recipeRepository.SaveChangesAsync();

            recipe.Item = item;
            recipe.InventoryItem = inventoryItem;
            recipe.Unit = recipeUnit;

            return Mapper.Map<RecipeDto>(recipe);
        }

        public async Task<DeleteOrDeactivateResponse> ActivateAsync(int restaurantId, int itemId, int recipeId)
        {
            await EnsureRecipeContextAsync(restaurantId, itemId);
            var recipe = await GetRecipeForMutationAsync(restaurantId, itemId, recipeId);

            recipe.IsActive = true;
            await _recipeRepository.SaveChangesAsync();

            return Mapper.Map<DeleteOrDeactivateResponse>(recipe);
        }

        public async Task<DeleteOrDeactivateResponse> DeactivateAsync(int restaurantId, int itemId, int recipeId)
        {
            await EnsureRecipeContextAsync(restaurantId, itemId);
            var recipe = await GetRecipeForMutationAsync(restaurantId, itemId, recipeId);

            recipe.IsActive = false;
            await _recipeRepository.SaveChangesAsync();

            return Mapper.Map<DeleteOrDeactivateResponse>(recipe);
        }

        public async Task<DeleteOrDeactivateResponse> DeleteAsync(int restaurantId, int itemId, int recipeId)
        {
            await EnsureRecipeContextAsync(restaurantId, itemId);
            var recipe = await GetRecipeForMutationAsync(restaurantId, itemId, recipeId);

            recipe.IsActive = false;
            await _recipeRepository.Delete(recipe);
            await _recipeRepository.SaveChangesAsync();

            return Mapper.Map<DeleteOrDeactivateResponse>(recipe);
        }

        private async Task<Domain.Entities.Item> EnsureRecipeContextAsync(int restaurantId, int itemId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);

            if (itemId <= 0)
                throw new BusinessRuleException("Invalid item ID.");

            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new NotFoundException(ErrorCode.RestaurantNotFound);

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var item = await _itemRepository
                .Query(x => x.Id == itemId && x.RestaurantId == restaurantId && x.IsActive)
                .FirstOrDefaultAsync();

            return item ?? throw new NotFoundException("Active item not found.");
        }

        private async Task<Domain.Entities.Recipe> GetRecipeForMutationAsync(
            int restaurantId,
            int itemId,
            int recipeId)
        {
            if (recipeId <= 0)
                throw new BusinessRuleException("Invalid recipe ID.");

            var recipe = await _recipeRepository.GetByIdForItemAsync(restaurantId, itemId, recipeId);
            return recipe ?? throw new NotFoundException("Recipe not found.");
        }

        private async Task<Domain.Entities.InventoryItem> GetActiveInventoryItemAsync(
            int restaurantId,
            int inventoryItemId)
        {
            if (inventoryItemId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidInventoryItemId);

            var inventoryItem = await _inventoryItemRepository.GetInventoryByRestaurantIdAsync(inventoryItemId, restaurantId);
            if (inventoryItem is null || !inventoryItem.IsActive)
                throw new NotFoundException(ErrorCode.InventoryItemNotFound);

            return inventoryItem;
        }

        private async Task<Unit> GetUnitAsync(int unitId)
        {
            if (unitId <= 0)
                throw new BusinessRuleException("Invalid unit ID.");

            var unit = await _unitRepository.GetByIdAsync(unitId);
            return unit ?? throw new NotFoundException(ErrorCode.UnitNotFound);
        }

        private static void EnsureUnitConversionAllowed(Unit recipeUnit, Unit stockUnit)
        {
            if (GetBaseUnitId(recipeUnit) != GetBaseUnitId(stockUnit))
                throw new BusinessRuleException(ErrorCode.InventoryUnitConversionNotAllowed);
        }

        private static int GetBaseUnitId(Unit unit)
            => unit.BaseUnitId ?? unit.Id;
    }
}
