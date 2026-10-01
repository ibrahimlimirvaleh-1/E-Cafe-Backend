using ECafe.Application.Features.Commands.Auth.RevokeSession;
using ECafe.Application.Features.Queries.Auth.GetMySessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class UserSessionController : BaseController
{
    [HttpGet(ApiRoutes.UserSession.GetMySessions)]
    public async Task<IActionResult> GetMySessions()
    {
        var result = await Mediator.Send(new GetMySessionsQuery());
        return Ok(result);
    }

    [HttpDelete(ApiRoutes.UserSession.RevokeSession)]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        await Mediator.Send(new RevokeSessionCommand(sessionId));
        return NoContent();
    }
}
