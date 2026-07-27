using ECafe.Application.DTOs.Notification;
using ECafe.Application.Services.Notification.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Notification
{
    public class GetMyNotificationsQuery : IRequest<List<NotificationResponse>>
    {
        public class Handler : IRequestHandler<GetMyNotificationsQuery, List<NotificationResponse>>
        {
            private readonly INotificationService _notificationService;

            public Handler(INotificationService notificationService)
            {
                _notificationService = notificationService;
            }

            public Task<List<NotificationResponse>> Handle(
                GetMyNotificationsQuery request,
                CancellationToken cancellationToken)
                => _notificationService.GetMyNotificationsAsync();
        }
    }
}
