using ECafe.Application.Features.Queries.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

public class WorkflowController : BaseController
{
    [Authorize]
    [HttpGet(ApiRoutes.Workflow.GetActions)]
    public async Task<IActionResult> GetActions(
        string flowCode,
        [FromQuery] int statusId,
        [FromQuery] int? restaurantId,
        [FromQuery] int? entityId)
        => Ok(await Mediator.Send(new GetWorkflowActionsQuery
        {
            FlowCode = flowCode,
            StatusId = statusId,
            RestaurantId = restaurantId,
            EntityId = entityId
        }));
}
