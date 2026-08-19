using ECafe.Application.Features.Commands.Developer.SendTestEmail;
using ECafe.Application.Features.Commands.Developer.SendTestSms;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[HasPermission(PermissionCode.ViewAuditLogs)]
public sealed class DeveloperNotificationTestController : BaseController
{
    private readonly IWebHostEnvironment _environment;

    public DeveloperNotificationTestController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("api/v1/developer/test/email")]
    public async Task<IActionResult> SendEmail([FromBody] SendTestEmailCommand command)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(command));
    }

    [HttpPost("api/v1/developer/test/sms")]
    public async Task<IActionResult> SendSms([FromBody] SendTestSmsCommand command)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(command));
    }

    private void EnsureDeveloperTestEndpointIsAllowed()
    {
        if (_environment.IsProduction())
            throw new ForbiddenException("Developer test endpoints are disabled in production.");
    }
}
