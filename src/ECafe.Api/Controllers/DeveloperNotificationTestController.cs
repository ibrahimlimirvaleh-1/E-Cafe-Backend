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

    [HttpPost(ApiRoutes.DeveloperNotificationTest.SendEmail)]
    public async Task<IActionResult> SendEmail([FromBody] SendTestEmailCommand command)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(command));
    }

    [HttpPost(ApiRoutes.DeveloperNotificationTest.SendSms)]
    public async Task<IActionResult> SendSms([FromBody] SendTestSmsCommand command)
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(command));
    }

    [HttpGet(ApiRoutes.DeveloperNotificationTest.GetSmsBalance)]
    public async Task<IActionResult> GetSmsBalance()
    {
        EnsureDeveloperTestEndpointIsAllowed();
        return Ok(await Mediator.Send(new GetSmsBalanceQuery()));
    }

    [HttpGet(ApiRoutes.DeveloperNotificationTest.GetSmsStatus)]
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