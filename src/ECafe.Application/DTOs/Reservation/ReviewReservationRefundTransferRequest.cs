namespace ECafe.Application.DTOs.Reservation;

public sealed class ConfirmReservationRefundTransferRequest
{
    public int TransferId { get; init; }
}

public sealed class DisputeReservationRefundTransferRequest
{
    public int TransferId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
