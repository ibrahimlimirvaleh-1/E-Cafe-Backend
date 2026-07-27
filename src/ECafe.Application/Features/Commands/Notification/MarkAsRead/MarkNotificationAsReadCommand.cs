using ECafe.Application.Services.Notification.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Notification.MarkAsRead
{
    public class MarkNotificationAsReadCommand : IRequest
    {
        public int NotificationId { get; set; }

        public class Handler : IRequestHandler<MarkNotificationAsReadCommand>
        {
            private readonly INotificationService _notificationService;

            public Handler(INotificationService notificationService)
            {
                _notificationService = notificationService;
            }

            public Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
                => _notificationService.MarkAsReadAsync(request.NotificationId);
        }
    }
}
