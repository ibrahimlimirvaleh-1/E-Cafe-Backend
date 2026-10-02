using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class RestaurantScheduleFlowController(IRestaurantScheduleService service) : BaseController
{
    [HasPermission(PermissionCode.EditRestaurantInfo)]
    [HttpPost(ApiRoutes.RestaurantScheduleFlow.Propose)]
    public async Task<IActionResult> Propose(int restaurantId, [FromBody] ProposeScheduleRequest request, CancellationToken cancellationToken)
        => Ok(await service.ProposeAsync(restaurantId, request, cancellationToken));

    [HasPermission(PermissionCode.EditRestaurantInfo)]
    [HttpPost(ApiRoutes.RestaurantScheduleFlow.Apply)]
    public async Task<IActionResult> Apply(int restaurantId, [FromBody] ScheduleActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApplyAsync(restaurantId, request, cancellationToken));

    [HasPermission(PermissionCode.EditRestaurantInfo)]
    [HttpPost(ApiRoutes.RestaurantScheduleFlow.Withdraw)]
    public async Task<IActionResult> Withdraw(int restaurantId, [FromBody] ScheduleActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.WithdrawAsync(restaurantId, request, cancellationToken));

    [HasPermission(PermissionCode.EditRestaurantInfo)]
    [HttpPost(ApiRoutes.RestaurantScheduleFlow.AcknowledgeSession)]
    public async Task<IActionResult> AcknowledgeSession(int restaurantId, int consentId,
        [FromBody] ScheduleDecisionRequest request, CancellationToken cancellationToken)
        => Ok(await service.AcknowledgeSessionAsync(restaurantId, consentId, request, cancellationToken));

    [Authorize(Roles = "5")]
    [HttpPost(ApiRoutes.RestaurantScheduleFlow.Respond)]
    public async Task<IActionResult> Respond(int reservationId, [FromBody] ScheduleDecisionRequest request, CancellationToken cancellationToken)
        => Ok(await service.RespondAsync(reservationId, request, cancellationToken));
}
