using System.ComponentModel.DataAnnotations;
using ECafe.Domain.Enums;

namespace ECafe.Application.DTOs.Reservation;

public sealed class ReservationDepartureRequest
{
    public ReservationDepartureReason ReasonCode { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReservationTableChangeRequest
{
    [Range(1, int.MaxValue)]
    public int TableId { get; set; }
    public bool CustomerConsent { get; set; }
}
