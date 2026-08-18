using ECafe.Application.Features.Commands.Outbox;
using ECafe.Application.Features.Queries.Outbox;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class OutboxController : BaseController
    {
        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpGet("api/v1/admin/outbox/messages")]
        public async Task<IActionResult> GetMessages([FromQuery] GetOutboxMessagesQuery query)
            => Ok(await Mediator.Send(query));

        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpGet("api/v1/admin/outbox/messages/{id:guid}")]
        public async Task<IActionResult> GetMessage(Guid id)
            => Ok(await Mediator.Send(new GetOutboxMessageByIdQuery(id)));

        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpPost("api/v1/admin/outbox/messages/{id:guid}/retry")]
        public async Task<IActionResult> Retry(Guid id)
            => Ok(await Mediator.Send(new RetryOutboxMessageCommand(id)));
    }
}
