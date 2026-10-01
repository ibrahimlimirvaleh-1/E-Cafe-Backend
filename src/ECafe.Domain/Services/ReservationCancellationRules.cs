using ECafe.Domain.Entities;
using ECafe.Domain.Enums;

namespace ECafe.Domain.Services;

public static class ReservationCancellationRules
{
    public static bool CanRefund(Reservation reservation, DateTime nowUtc, bool initiatedByCustomer)
        => reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
           reservation.SeatedAt == null &&
           reservation.PaymentProofs.Any(proof => proof.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed)) &&
           (!initiatedByCustomer || IsWithinRefundWindow(reservation, nowUtc));

    public static DateTime GetGraceDeadline(DateTime reservedAt, DateTime confirmedAt, int graceMinutes)
    {
        var graceDeadline = confirmedAt.AddMinutes(Math.Max(graceMinutes, 0));
        return graceDeadline < reservedAt ? graceDeadline : reservedAt;
    }

    public static bool IsWithinRefundWindow(Reservation reservation, DateTime nowUtc)
        => nowUtc < GetRefundDeadline(reservation);

    public static DateTime GetRefundDeadline(Reservation reservation)
    {
        var deadline = reservation.CancellationDeadline ?? reservation.ReservedAt;
        if (reservation.CancellationGraceDeadlineAt.HasValue && reservation.CancellationGraceDeadlineAt > deadline)
            deadline = reservation.CancellationGraceDeadlineAt.Value;
        return deadline < reservation.ReservedAt ? deadline : reservation.ReservedAt;
    }
}
