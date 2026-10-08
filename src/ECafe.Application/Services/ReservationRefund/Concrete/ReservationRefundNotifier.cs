using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Domain.Enums;
using ReservationEntity = ECafe.Domain.Entities.Reservation;
using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ReservationRefundTransferEntity = ECafe.Domain.Entities.ReservationRefundTransfer;

namespace ECafe.Application.Services.ReservationRefund.Concrete;

public sealed class ReservationRefundNotifier : IReservationRefundNotifier
{
    private static readonly int[] RestaurantManagerRoleIds =
    [
        (int)RoleCode.Manager,
        (int)RoleCode.Owner
    ];

    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly INotificationService _notificationService;

    public ReservationRefundNotifier(
        IUserRestaurantRepository userRestaurantRepository,
        INotificationService notificationService)
    {
        _userRestaurantRepository = userRestaurantRepository;
        _notificationService = notificationService;
    }

    public async Task NotifyRestaurantAsync(
        ReservationEntity reservation,
        ReservationRefundEntity refund,
        NotificationType notificationType,
        string title,
        string message)
    {
        var assignments = await _userRestaurantRepository.GetActiveByRestaurantAndRolesAsync(
            reservation.RestaurantId,
            RestaurantManagerRoleIds);

        if (assignments.Count == 0)
        {
            var ownerAssignment = await _userRestaurantRepository
                .GetActiveOwnerByRestaurantAsync(reservation.RestaurantId);

            if (ownerAssignment is not null)
                assignments.Add(ownerAssignment);
        }

        foreach (var assignment in assignments)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.UserId,
                RestaurantId = reservation.RestaurantId,
                Title = title,
                Message = message,
                TypeId = (int)notificationType,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = SerializePayload(reservation.RestaurantId, reservation.Id, refund),
                RelatedEntityType = AuditEntityTypes.ReservationRefund,
                RelatedEntityId = refund.Id
            });
        }
    }

    public Task NotifyCustomerTransferSubmittedAsync(
        ReservationRefundEntity refund,
        ReservationRefundTransferEntity transfer)
    {
        return _notificationService.CreateAsync(new CreateNotificationRequest
        {
            UserId = refund.Reservation.CustomerUserId,
            RestaurantId = refund.Reservation.RestaurantId,
            Title = "Geri ödəniş çeki göndərildi",
            Message = $"Rezervasiya #{refund.ReservationId} üçün {transfer.Amount:0.00} {refund.CurrencyCode} geri ödəniş çeki göndərildi. Zəhmət olmasa yoxlayın.",
            TypeId = (int)NotificationType.ReservationRefundTransferSubmitted,
            ChannelId = (int)NotificationChannel.InApp,
            PayloadJson = SerializePayload(refund.Reservation.RestaurantId, refund.ReservationId, refund),
            RelatedEntityType = AuditEntityTypes.ReservationRefund,
            RelatedEntityId = refund.Id
        });
    }

    // Refund bildirişinə lazım olan identifikatorları payload kimi yığır.
    private static string SerializePayload(int restaurantId, int reservationId, ReservationRefundEntity refund)
    {
        return JsonSerializer.Serialize(new
        {
            restaurantId,
            reservationId,
            refundId = refund.Id,
            refundStatusId = refund.StatusId
        });
    }
}
