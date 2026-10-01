using ECafe.Application.DTOs.Reservation;

namespace ECafe.Application.Services.Reservation.Abstract;

public interface IReservationArrivalService
{
    Task<ReservationArrivalOptionsResponse> GetOptionsAsync(int reservationId, CancellationToken cancellationToken);
    Task<ReservationArrivalOfferResponse> OfferAsync(int reservationId, DateTimeOffset arrivalAt, CancellationToken cancellationToken);
    Task<ReservationArrivalOfferResponse> AcceptAsync(int reservationId, Guid consentToken, CancellationToken cancellationToken);
}
