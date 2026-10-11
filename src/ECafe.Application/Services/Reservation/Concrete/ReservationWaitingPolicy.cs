using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Enums;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.Reservation.Concrete;

public static class ReservationWaitingPolicy
{
    /// <summary>Faktiki gəlmiş, amma hələ əyləşməmiş aktiv rezervasiyanı müəyyən edir.</summary>
    public static bool IsWaiting(ReservationEntity reservation)
        => reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
           reservation.ArrivedAt.HasValue && reservation.SeatedAt == null;

    /// <summary>Ümumi gözləməni və erkən gəlişi gecikmə saymadan xidmət gecikməsini hesablayır.</summary>
    public static ReservationWaitingInfo? GetInfo(ReservationEntity reservation, DateTime nowUtc, int thresholdMinutes)
    {
        if (!IsWaiting(reservation))
            return null;

        var arrivedAt = reservation.ArrivedAt!.Value;
        var serviceStart = arrivedAt > reservation.ReservedAt ? arrivedAt : reservation.ReservedAt;
        var waitingMinutes = Math.Max(0, (int)(nowUtc - arrivedAt).TotalMinutes);
        var delayMinutes = Math.Max(0, (int)(nowUtc - serviceStart).TotalMinutes);
        return new(new DateTimeOffset(DateTime.SpecifyKind(arrivedAt, DateTimeKind.Utc)),
            waitingMinutes, delayMinutes, nowUtc >= serviceStart.AddMinutes(thresholdMinutes));
    }
}
