using ECafe.Application.DTOs.Recipe;
using ECafe.Application.Features.Commands.Recipe.Activate;
using ECafe.Application.Features.Commands.Recipe.Create;
using ECafe.Application.Features.Commands.Recipe.Deactivate;
using ECafe.Application.Features.Commands.Recipe.Delete;
using ECafe.Application.Features.Commands.Recipe.Update;
using ECafe.Application.Features.Queries.Recipe.GetByItem;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class RecipeController : BaseController
    {
        [HasPermission(PermissionCode.ViewRecipes)]
        [HttpGet("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes")]
        public async Task<IActionResult> GetByItem(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId)
        {
            var query = new GetRecipesByItemQuery
            {
                RestaurantId = restaurantId,
                ItemId = itemId
            };

            return Ok(await Mediator.Send(query));
        }

        [HasPermission(PermissionCode.ManageRecipes)]
        [HttpPost("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes")]
        public async Task<IActionResult> Create(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId,
            [FromBody] CreateRecipeRequest request)
        {
            var command = new CreateRecipeCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                InventoryItemId = request.InventoryItemId,
                Quantity = request.Quantity,
                UnitId = request.UnitId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageRecipes)]
        [HttpPut("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}")]
        public async Task<IActionResult> Update(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId,
            [FromRoute] int recipeId,
            [FromBody] UpdateRecipeRequest request)
        {
            var command = new UpdateRecipeCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                RecipeId = recipeId,
                InventoryItemId = request.InventoryItemId,
                Quantity = request.Quantity,
                UnitId = request.UnitId,
                IsActive = request.IsActive
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageRecipes)]
        [HttpPatch("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/activate")]
        public async Task<IActionResult> Activate(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId,
            [FromRoute] int recipeId)
        {
            var command = new ActivateRecipeCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                RecipeId = recipeId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageRecipes)]
        [HttpPatch("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/deactivate")]
        public async Task<IActionResult> Deactivate(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId,
            [FromRoute] int recipeId)
        {
            var command = new DeactivateRecipeCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                RecipeId = recipeId
            };

            return Ok(await Mediator.Send(command));
        }

        [HasPermission(PermissionCode.ManageRecipes)]
        [HttpDelete("api/v1/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}")]
        public async Task<IActionResult> Delete(
            [FromRoute] int restaurantId,
            [FromRoute] int itemId,
            [FromRoute] int recipeId)
        {
            var command = new DeleteRecipeCommand
            {
                RestaurantId = restaurantId,
                ItemId = itemId,
                RecipeId = recipeId
            };

            return Ok(await Mediator.Send(command));
        }
    }
}
