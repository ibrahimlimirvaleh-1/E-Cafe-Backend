using ECafe.Application.Features.Commands.Developer.SendTestEmail;
using ECafe.Application.Features.Commands.Developer.SendTestSms;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.Queries.Developer.GetSmsBalance;
using ECafe.Application.Features.Queries.Developer.GetSmsStatus;
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

    [HttpGet("api/v1/developer/test/sms/balance")]
    public async Task<IActionResult> GetSmsBalance()
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(new GetSmsBalanceQuery()));
    }

    [HttpGet("api/v1/developer/test/sms/status/{messageId}")]
    public async Task<IActionResult> GetSmsStatus([FromRoute] string messageId)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(new GetSmsStatusQuery
        {
            MessageId = messageId
        }));
    }

    private void EnsureDeveloperTestEndpointIsAllowed()
    {
        if (_environment.IsProduction())
            throw new ForbiddenException("Developer test endpoints are disabled in production.");
    }
}
