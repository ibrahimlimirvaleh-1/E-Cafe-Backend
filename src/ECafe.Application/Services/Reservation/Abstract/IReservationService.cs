using ECafe.Application.DTOs.Reservation;

namespace ECafe.Application.Services.Reservation.Abstract
{
    public interface IReservationService
    {
        Task<ECafe.Application.DTOs.Reservation.ReservationResponse> GetReservationByIdAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<ECafe.Application.DTOs.Reservation.ReservationResponse> CreateReservationAsync(
            int restaurantId,
            CreateReservationRequest request,
            CancellationToken cancellationToken = default);

        Task<int> ExpirePendingReservationsAsync(int batchSize,CancellationToken cancellationToken);
    }
}
