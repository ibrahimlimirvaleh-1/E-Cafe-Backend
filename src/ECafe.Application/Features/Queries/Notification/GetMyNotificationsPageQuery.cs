using ECafe.Application.DTOs.Notification;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Shared.DTOs;
using MediatR;

namespace ECafe.Application.Features.Queries.Notification;

public sealed class GetMyNotificationsPageQuery : IRequest<PaginatedList<NotificationResponse>>
{
    public PaginationFilter Pagination { get; init; } = new();

    public sealed class Handler(INotificationService notifications)
        : IRequestHandler<GetMyNotificationsPageQuery, PaginatedList<NotificationResponse>>
    {
        public Task<PaginatedList<NotificationResponse>> Handle(GetMyNotificationsPageQuery request, CancellationToken cancellationToken)
            => notifications.GetMyNotificationsPageAsync(request.Pagination, cancellationToken);
    }
}
