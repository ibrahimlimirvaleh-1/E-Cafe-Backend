using ECafe.Application.DTOs.Notification;
using ECafe.Application.Services.Notification.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Notification
{
    public class GetUnreadNotificationCountQuery : IRequest<UnreadNotificationCountResponse>
    {
        public class Handler : IRequestHandler<GetUnreadNotificationCountQuery, UnreadNotificationCountResponse>
        {
            private readonly INotificationService _notificationService;

            public Handler(INotificationService notificationService)
            {
                _notificationService = notificationService;
            }

            public Task<UnreadNotificationCountResponse> Handle(
                GetUnreadNotificationCountQuery request,
                CancellationToken cancellationToken)
                => _notificationService.GetUnreadCountAsync();
        }
    }
}
