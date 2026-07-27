using ECafe.Application.Features.Commands.Notification.MarkAllAsRead;
using ECafe.Application.Features.Commands.Notification.MarkAsRead;
using ECafe.Application.Features.Queries.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class NotificationController : BaseController
    {
        [Authorize]
        [HttpGet("api/v1/notifications")]
        public async Task<IActionResult> GetMine()
            => Ok(await Mediator.Send(new GetMyNotificationsQuery()));

        [Authorize]
        [HttpGet("api/v1/notifications/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
            => Ok(await Mediator.Send(new GetUnreadNotificationCountQuery()));

        [Authorize]
        [HttpPost("api/v1/notifications/{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            await Mediator.Send(new MarkNotificationAsReadCommand
            {
                NotificationId = notificationId
            });

            return Ok();
        }

        [Authorize]
        [HttpPost("api/v1/notifications/read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await Mediator.Send(new MarkAllNotificationsAsReadCommand());
            return Ok();
        }
    }
}
