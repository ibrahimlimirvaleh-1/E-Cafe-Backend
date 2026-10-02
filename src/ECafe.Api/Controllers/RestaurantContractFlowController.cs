using ECafe.Application.Features.Commands.RestaurantContract;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantContractFlowController : BaseController
{
    [HasPermission(PermissionCode.ManageRestaurantContracts)]
    [HttpPost(ApiRoutes.RestaurantContractFlow.SendForSignature)]
    public async Task<IActionResult> SendForSignature(int restaurantId, int contractId)
    {
        await Mediator.Send(new SendRestaurantContractForSignatureCommand
        {
            RestaurantId = restaurantId,
            ContractId = contractId
        });

        return Ok();
    }

    [HasPermission(PermissionCode.ViewRestaurantContracts)]
    [HttpPost(ApiRoutes.RestaurantContractFlow.Approve)]
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

    [HasPermission(PermissionCode.ManageRestaurantContracts)]
    [HttpPost(ApiRoutes.RestaurantContractFlow.Activate)]
    public async Task<IActionResult> Activate(int restaurantId, int contractId)
    {
        await Mediator.Send(new ActivateRestaurantContractCommand
        {
            RestaurantId = restaurantId,
            ContractId = contractId
        });

        return Ok();
    }

    [HasPermission(PermissionCode.ManageRestaurantContracts)]
    [HttpPost(ApiRoutes.RestaurantContractFlow.Terminate)]
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
