using AutoMapper;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Features.MobileApp;
using ECafe.Application.Repositories.Notification;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using FluentValidation;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Notification.Concrete
{
    public class NotificationManager : BaseManager, INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IValidator<CreateNotificationRequest> _validator;
        private readonly INotificationChangeDispatcher _notificationDispatcher;
        private readonly IMobilePushOutboxWriter _mobilePushOutboxWriter;
        private readonly INotificationEmailOutboxWriter _notificationEmailOutboxWriter;
        private readonly IUserRestaurantRepository _assignments;
        private readonly Services.Localization.Abstract.INotificationTextService? _texts;

        public NotificationManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            INotificationRepository notificationRepository,
            IValidator<CreateNotificationRequest> validator,
            INotificationChangeDispatcher notificationDispatcher,
            IMobilePushOutboxWriter mobilePushOutboxWriter,
            INotificationEmailOutboxWriter notificationEmailOutboxWriter,
            IUserRestaurantRepository assignments,
            Services.Localization.Abstract.INotificationTextService? texts = null)
            : base(httpContextAccessor, mapper, configuration)
        {
            _notificationRepository = notificationRepository;
            _validator = validator;
            _notificationDispatcher = notificationDispatcher;
            _mobilePushOutboxWriter = mobilePushOutboxWriter;
            _notificationEmailOutboxWriter = notificationEmailOutboxWriter;
            _assignments = assignments;
            _texts = texts;
        }

        public async Task CreateAsync(CreateNotificationRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Notification request is required.");

            await _validator.ValidateAndThrowAsync(request);

            var notification = Mapper.Map<Domain.Entities.Notification>(request);
            if (_texts != null) await _texts.PrepareAsync(notification, HttpContextAccessor.HttpContext?.RequestAborted ?? default);

            await _notificationRepository.Add(notification);
            _mobilePushOutboxWriter.Enqueue(notification);
            await _notificationEmailOutboxWriter.EnqueueAsync(notification);
            await _notificationRepository.SaveChangesAsync();
            await _notificationDispatcher.NotifyChangedAsync(request.UserId);
        }

        public async Task<List<NotificationResponse>> GetMyNotificationsAsync()
        {
            var notifications = await _notificationRepository.GetByUserAsync(GetCurrentUserId(), await GetNotificationRestaurantScopeAsync());
            return MapForReader(notifications);
        }

        /// <summary>Keçərli səhifəni maksimum 100 qeydlə oxuyur; əvvəlki profil/istifadəçi yoxlaması və oxuyanın dili qorunur.</summary>
        public async Task<PaginatedList<NotificationResponse>> GetMyNotificationsPageAsync(PaginationFilter pagination,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = PaginationFilterNormalizer.Normalize(pagination);
            var notifications = await _notificationRepository.GetPageByUserAsync(GetCurrentUserId(),
                await GetNotificationRestaurantScopeAsync(), filter.PageNumber, filter.PageSize, cancellationToken);
            return new PaginatedList<NotificationResponse>(MapForReader(notifications.Items), notifications.TotalCount,
                filter.PageNumber, filter.PageSize);
        }

        /// <summary>Yalnız oxunmuş qeydləri DTO-ya çevirib oxuyanın dilinə uyğunlaşdırır; saxlanmış entity və oxunma vəziyyətini dəyişmir.</summary>
        private List<NotificationResponse> MapForReader(List<Domain.Entities.Notification> notifications)
        {
            var responses = Mapper.Map<List<NotificationResponse>>(notifications);
            if (_texts != null)
                for (var index = 0; index < notifications.Count; index++) _texts.LocalizeForReader(notifications[index], responses[index]);
            return responses;
        }

        public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync()
        {
            var count = await _notificationRepository.GetUnreadCountAsync(GetCurrentUserId(), await GetNotificationRestaurantScopeAsync());

            return new UnreadNotificationCountResponse
            {
                Count = count
            };
        }

        public async Task MarkAllAsReadAsync()
        {
            var unreadNotifications = await _notificationRepository.GetUnreadByUserAsync(GetCurrentUserId(), await GetNotificationRestaurantScopeAsync());
            if (unreadNotifications.Count == 0)
                return;

            foreach (var notification in unreadNotifications)
            {
                MarkAsRead(notification);
            }

            await _notificationRepository.SaveChangesAsync();
            await _notificationDispatcher.NotifyChangedAsync(GetCurrentUserId());
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            if (notificationId <= 0)
                throw new BusinessRuleException("Invalid notification ID.");

            var notification = await _notificationRepository.GetByUserAndIdTrackedAsync(
                GetCurrentUserId(),
                notificationId,
                await GetNotificationRestaurantScopeAsync());

            if (notification is null)
                throw new BusinessRuleException("Notification not found.");

            if (notification.IsRead && notification.StatusId == (int)NotificationStatus.Read)
                return;

            MarkAsRead(notification);
            await _notificationRepository.SaveChangesAsync();
            await _notificationDispatcher.NotifyChangedAsync(GetCurrentUserId());
        }

        /// <summary>Artıq oxunmuş bildirişin tarixini dəyişmədən oxunma vəziyyətini tamamlayır.</summary>
        private static void MarkAsRead(Domain.Entities.Notification notification)
        {
            notification.IsRead = true;
            notification.StatusId = (int)NotificationStatus.Read;
            notification.ReadAt ??= DateTime.UtcNow;
        }

        /// <summary>İşçinin seçilmiş restorana canlı aidiyyətini yoxlayır; adi müştərinin öz müxtəlif restoran bildirişlərini saxlayır.</summary>
        private async Task<int?> GetNotificationRestaurantScopeAsync()
        {
            if (IsCurrentUserSuperAdmin() || GetCurrentRoleId() == (int)RoleCode.Customer)
                return null;

            var restaurantId = GetRequiredCurrentRestaurantId();
            var role = await _assignments.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId);
            if (!role.HasValue || !ECafe.Domain.Policies.UserProfileAccessPolicy.IsRestaurantRole(role.Value))
                throw new ForbiddenException(ErrorCode.AccessDenied);

            return restaurantId;
        }
    }
}
