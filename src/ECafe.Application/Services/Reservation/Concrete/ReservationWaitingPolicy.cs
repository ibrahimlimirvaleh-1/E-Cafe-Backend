using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.Reservation.Concrete;

public static class ReservationWaitingPolicy
{
    /// <summary>Faktiki gəlmiş, amma hələ əyləşməmiş aktiv rezervasiyanı müəyyən edir.</summary>
    public static bool IsWaiting(ReservationEntity reservation)
        => reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
           reservation.ArrivedAt.HasValue && reservation.SeatedAt == null;

    /// <summary>Yalnız rezervasiyanın xidmət intervalında gözləməni hesablayır; bitmiş intervalı canlı gözləmə kimi göstərmir.</summary>
    public static ReservationWaitingInfo? GetInfo(ReservationEntity reservation, DateTime nowUtc, int thresholdMinutes)
    {
        if (!IsWaiting(reservation) || GetServiceEndUtc(reservation) is not { } endsAt || nowUtc >= endsAt)
            return null;

        var arrivedAt = reservation.ArrivedAt!.Value;
        var serviceStart = arrivedAt > reservation.ReservedAt ? arrivedAt : reservation.ReservedAt;
        var waitingMinutes = Math.Max(0, (int)(nowUtc - arrivedAt).TotalMinutes);
        var delayMinutes = Math.Max(0, (int)(nowUtc - serviceStart).TotalMinutes);
        return new(new DateTimeOffset(DateTime.SpecifyKind(arrivedAt, DateTimeKind.Utc)),
            waitingMinutes, delayMinutes, nowUtc >= serviceStart.AddMinutes(thresholdMinutes));
    }

    /// <summary>Gəlişi qeyd olunmuş açıq rezervasiyanın xidmət vaxtı bitdikdə və ya cədvəli müəyyən edilmədikdə nəticənin yoxlanmasını tələb edir.</summary>
    public static bool RequiresReceptionReview(ReservationEntity reservation, DateTime nowUtc)
        => IsWaiting(reservation) && nowUtc >= reservation.ReservedAt &&
           (GetServiceEndUtc(reservation) is not { } endsAt || nowUtc >= endsAt);

    /// <summary>Restoranın yerli saatı ilə gecəyarısını keçən xidmət intervalını tapır və razılaşdırılmış çıxış vaxtını yuxarı sərhəd kimi qoruyur.</summary>
    private static DateTime? GetServiceEndUtc(ReservationEntity reservation)
    {
        if (reservation.Restaurant is not { } restaurant)
            return null;
        var bookingLocal = RestaurantTimeZoneConverter.ToRestaurantLocalTime(
            new DateTimeOffset(DateTime.SpecifyKind(reservation.ReservedAt, DateTimeKind.Utc)), restaurant.TimeZone);
        if (!RestaurantWorkingHoursCalculator.TryGetActiveInterval(restaurant.WorkingHours, bookingLocal, out var interval))
            return null;
        var endsAt = interval.EndsAt.UtcDateTime;
        return reservation.MustVacateAt is { } vacateAt && vacateAt < endsAt ? vacateAt : endsAt;
    }
}
