using ECafe.Application.Features.Commands.Developer.SendTestEmail;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[HasPermission(PermissionCode.ViewAuditLogs)]
public sealed class DeveloperNotificationTestController : BaseController
{
    private readonly IWebHostEnvironment _environment;

    public DeveloperNotificationTestController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost(ApiRoutes.DeveloperNotificationTest.SendEmail)]
    public async Task<IActionResult> SendEmail([FromBody] SendTestEmailCommand command)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(command));
    }

    private void EnsureDeveloperTestEndpointIsAllowed()
    {
        if (_environment.IsProduction())
            throw new ForbiddenException(ErrorCode.DeveloperTestEndpointDisabled);
    }
}
