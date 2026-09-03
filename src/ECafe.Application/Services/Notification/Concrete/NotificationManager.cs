using AutoMapper;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.Notification;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Notification.Concrete
{
    public class NotificationManager : BaseManager, INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IValidator<CreateNotificationRequest> _validator;

        public NotificationManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            INotificationRepository notificationRepository,
            IValidator<CreateNotificationRequest> validator)
            : base(httpContextAccessor, mapper, configuration)
        {
            _notificationRepository = notificationRepository;
            _validator = validator;
        }

        public async Task CreateAsync(CreateNotificationRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Notification request is required.");

            await _validator.ValidateAndThrowAsync(request);

            var notification = Mapper.Map<Domain.Entities.Notification>(request);

            await _notificationRepository.Add(notification);
            await _notificationRepository.SaveChangesAsync();
        }

        public async Task<List<NotificationResponse>> GetMyNotificationsAsync()
        {
            var notifications = await _notificationRepository.GetByUserAsync(GetCurrentUserId(), GetNotificationRestaurantScope());
            return Mapper.Map<List<NotificationResponse>>(notifications);
        }

        public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync()
        {
            var count = await _notificationRepository.GetUnreadCountAsync(GetCurrentUserId(), GetNotificationRestaurantScope());

            return new UnreadNotificationCountResponse
            {
                Count = count
            };
        }

        public async Task MarkAllAsReadAsync()
        {
            var unreadNotifications = await _notificationRepository.GetUnreadByUserAsync(GetCurrentUserId(), GetNotificationRestaurantScope());
            if (unreadNotifications.Count == 0)
                return;

            foreach (var notification in unreadNotifications)
            {
                MarkAsRead(notification);
            }

            await _notificationRepository.SaveChangesAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            if (notificationId <= 0)
                throw new BusinessRuleException("Invalid notification ID.");

            var notification = await _notificationRepository.GetByUserAndIdTrackedAsync(
                GetCurrentUserId(),
                notificationId,
                GetNotificationRestaurantScope());

            if (notification is null)
                throw new BusinessRuleException("Notification not found.");

            if (notification.IsRead && notification.StatusId == (int)NotificationStatus.Read)
                return;

            MarkAsRead(notification);
            await _notificationRepository.SaveChangesAsync();
        }

        private static void MarkAsRead(Domain.Entities.Notification notification)
        {
            notification.IsRead = true;
            notification.StatusId = (int)NotificationStatus.Read;
            notification.ReadAt ??= DateTime.UtcNow;
        }

        private int? GetNotificationRestaurantScope()
            => IsCurrentUserSuperAdmin() ? null : GetRequiredCurrentRestaurantId();
    }
}