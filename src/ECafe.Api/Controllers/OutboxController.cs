using ECafe.Application.Features.Commands.Outbox;
using ECafe.Application.Features.Queries.Outbox;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers
{
    public class OutboxController : BaseController
    {
        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpGet(ApiRoutes.Outbox.GetMessages)]
        public async Task<IActionResult> GetMessages([FromQuery] GetOutboxMessagesQuery query)
            => Ok(await Mediator.Send(query));

        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpGet(ApiRoutes.Outbox.GetMessage)]
        public async Task<IActionResult> GetMessage(Guid id)
            => Ok(await Mediator.Send(new GetOutboxMessageByIdQuery(id)));

        [HasPermission(PermissionCode.ViewAuditLogs)]
        [HttpPost(ApiRoutes.Outbox.Retry)]
        public async Task<IActionResult> Retry(Guid id)
            => Ok(await Mediator.Send(new RetryOutboxMessageCommand(id)));
    }
}
