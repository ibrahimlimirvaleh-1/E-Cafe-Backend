using ECafe.Application.Features.Commands.Notification.MarkAllAsRead;
using ECafe.Application.Features.Commands.Notification.MarkAsRead;
using ECafe.Application.Features.Queries.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECafe.Shared.DTOs;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers
{
    public class NotificationController : BaseController
    {
        [Authorize]
        [HttpGet(ApiRoutes.Notification.GetMine)]
        public async Task<IActionResult> GetMine()
            => Ok(await Mediator.Send(new GetMyNotificationsQuery()));

        [Authorize]
        [HttpGet(ApiRoutes.Notification.GetMinePaged)]
        public async Task<IActionResult> GetMinePaged([FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
            => Ok(await Mediator.Send(new GetMyNotificationsPageQuery { Pagination = pagination }, cancellationToken));

        [Authorize]
        [HttpGet(ApiRoutes.Notification.GetUnreadCount)]
        public async Task<IActionResult> GetUnreadCount()
            => Ok(await Mediator.Send(new GetUnreadNotificationCountQuery()));

        [Authorize]
        [HttpPost(ApiRoutes.Notification.MarkAsRead)]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            await Mediator.Send(new MarkNotificationAsReadCommand
            {
                NotificationId = notificationId
            });

            return Ok();
        }

        [Authorize]
        [HttpPost(ApiRoutes.Notification.MarkAllAsRead)]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await Mediator.Send(new MarkAllNotificationsAsReadCommand());
            return Ok();
        }
    }
}
