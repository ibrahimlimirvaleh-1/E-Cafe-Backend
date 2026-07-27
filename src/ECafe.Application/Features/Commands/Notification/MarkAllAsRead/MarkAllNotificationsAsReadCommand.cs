using ECafe.Application.Services.Notification.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Notification.MarkAllAsRead
{
    public class MarkAllNotificationsAsReadCommand : IRequest
    {
        public class Handler : IRequestHandler<MarkAllNotificationsAsReadCommand>
        {
            private readonly INotificationService _notificationService;

            public Handler(INotificationService notificationService)
            {
                _notificationService = notificationService;
            }

            public Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
                => _notificationService.MarkAllAsReadAsync();
        }
    }
}
