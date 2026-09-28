using ECafe.Application.Features.Commands.RestaurantContract;
using ECafe.Application.Features.Queries.RestaurantContract;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class RestaurantContractController : BaseController
    {
        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurantContracts)]
        [HttpPost(ApiRoutes.RestaurantContract.Create)]
        public async Task<IActionResult> Create(int restaurantId, [FromBody] CreateRestaurantContractCommand command)
        {
            command.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(command));
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurantContracts)]
        [HttpPut(ApiRoutes.RestaurantContract.Update)]
        public async Task<IActionResult> Update(int restaurantId, int contractId, [FromBody] UpdateRestaurantContractCommand command)
        {
            command.RestaurantId = restaurantId;
            command.ContractId = contractId;
            await Mediator.Send(command);
            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantContracts)]
        [HttpGet(ApiRoutes.RestaurantContract.GetByRestaurant)]
        public async Task<IActionResult> GetByRestaurant(int restaurantId)
            => Ok(await Mediator.Send(new GetRestaurantContractsQuery(restaurantId)));

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantContracts)]
        [HttpGet(ApiRoutes.RestaurantContract.GetPagedByRestaurant)]
        public async Task<IActionResult> GetPagedByRestaurant(int restaurantId, [FromQuery] GetPagedRestaurantContractsQuery query)
        {
            query.RestaurantId = restaurantId;
            return Ok(await Mediator.Send(query));
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantContracts)]
        [HttpGet(ApiRoutes.RestaurantContract.GetActive)]
        public async Task<IActionResult> GetActive(int restaurantId)
            => Ok(await Mediator.Send(new GetActiveRestaurantContractQuery(restaurantId)));

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantContracts)]
        [HttpGet(ApiRoutes.RestaurantContract.GetActions)]
        public async Task<IActionResult> GetActions(int restaurantId, int contractId)
            => Ok(await Mediator.Send(new GetRestaurantContractActionsQuery(restaurantId, contractId)));

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurantContracts)]
        [HttpPost(ApiRoutes.RestaurantContract.SendForSignature)]
        public async Task<IActionResult> SendForSignature(int restaurantId, int contractId)
        {
            await Mediator.Send(new SendRestaurantContractForSignatureCommand
            {
                RestaurantId = restaurantId,
                ContractId = contractId
            });

            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ViewRestaurantContracts)]
        [HttpPost(ApiRoutes.RestaurantContract.Approve)]
        public async Task<IActionResult> Approve(
            int restaurantId,
            int contractId,
            [FromBody] ApproveRestaurantContractCommand? command)
        {
            command ??= new ApproveRestaurantContractCommand();
            command.RestaurantId = restaurantId;
            command.ContractId = contractId;
            await Mediator.Send(command);

            return Ok();
        }

        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurantContracts)]
        [HttpPost(ApiRoutes.RestaurantContract.Activate)]
        public async Task<IActionResult> Activate(int restaurantId, int contractId)
        {
            await Mediator.Send(new ActivateRestaurantContractCommand
            {
                RestaurantId = restaurantId,
                ContractId = contractId
            });

            return Ok();
        }


        [HasPermission(Domain.Enums.PermissionCode.ManageRestaurantContracts)]
        [HttpPost(ApiRoutes.RestaurantContract.Terminate)]
        public async Task<IActionResult> Terminate(int restaurantId, int contractId)
        {
            await Mediator.Send(new TerminateRestaurantContractCommand
            {
                RestaurantId = restaurantId,
                ContractId = contractId
            });

            return Ok();
        }
    }
}