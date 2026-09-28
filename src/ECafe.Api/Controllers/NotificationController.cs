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
        [HttpGet(ApiRoutes.Notification.GetMine)]
        public async Task<IActionResult> GetMine()
            => Ok(await Mediator.Send(new GetMyNotificationsQuery()));

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