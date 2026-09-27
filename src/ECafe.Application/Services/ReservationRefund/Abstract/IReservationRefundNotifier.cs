using ECafe.Domain.Enums;
using ReservationEntity = ECafe.Domain.Entities.Reservation;
using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ReservationRefundTransferEntity = ECafe.Domain.Entities.ReservationRefundTransfer;

namespace ECafe.Application.Services.ReservationRefund.Abstract;

public interface IReservationRefundNotifier
{
    Task NotifyRestaurantAsync(
        ReservationEntity reservation,
        ReservationRefundEntity refund,
        NotificationType notificationType,
        string title,
        string message);

    Task NotifyCustomerTransferSubmittedAsync(
        ReservationRefundEntity refund,
        ReservationRefundTransferEntity transfer);
}
