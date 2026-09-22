using ECafe.Application.DTOs.Reservation;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.Reservation.Abstract;

public interface IReservationService
{
    Task<ReservationResponse> GetReservationByIdAsync(
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationHistoryResponse> GetReservationHistoryAsync(
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<ReservationResponse>> GetMyReservationsAsync(
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<ReservationResponse>> GetRestaurantReservationsAsync(
        int restaurantId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> GetRestaurantReservationByIdAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationHistoryResponse> GetRestaurantReservationHistoryAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationResponse> CreateReservationAsync(
        int restaurantId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ExpirePendingReservationsAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task<PaymentInstructionResponse> SendPaymentInstructionAsync(
        int restaurantId,
        int reservationId,
        PaymentInstructionRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentProofResponse> SubmitPaymentProofAsync(
        int restaurantId,
        int reservationId,
        int fileId,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResponse> ApprovePaymentProofAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResponse> RejectPaymentProofAsync(
        int restaurantId,
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResponse> CancelReservationAsync(
        int reservationId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ReservationActionResponse> CancelRestaurantReservationAsync(
        int restaurantId,
        int reservationId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task EnsurePaymentProofCanBeSubmittedAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default);

}
