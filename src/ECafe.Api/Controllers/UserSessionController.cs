using ECafe.Application.Features.Commands.Auth.RevokeSession;
using ECafe.Application.Features.Queries.Auth.GetMySessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[Authorize]
public sealed class UserSessionController : BaseController
{
    [HttpGet("api/v1/user/sessions")]
    public async Task<IActionResult> GetMySessions()
    {
        var result = await Mediator.Send(new GetMySessionsQuery());
        return Ok(result);
    }

    [HttpDelete("api/v1/user/sessions/{sessionId}")]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        await Mediator.Send(new RevokeSessionCommand(sessionId));
        return NoContent();
    }
}
