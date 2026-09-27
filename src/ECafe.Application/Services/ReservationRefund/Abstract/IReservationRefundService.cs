using ECafe.Application.DTOs.Reservation;

namespace ECafe.Application.Services.ReservationRefund.Abstract;

public interface IReservationRefundService
{
    Task<ReservationRefundResponse?> GetMyByReservationAsync(
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationRefundResponse?> GetRestaurantByReservationAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationRefundResponse> RequestAsync(
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationRefundResponse> SubmitPayoutDetailsAsync(
        int refundId,
        string details,
        CancellationToken cancellationToken = default);

    Task<RestaurantReservationRefundPayoutDetailsResponse> GetPayoutDetailsForRestaurantAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default);
}
