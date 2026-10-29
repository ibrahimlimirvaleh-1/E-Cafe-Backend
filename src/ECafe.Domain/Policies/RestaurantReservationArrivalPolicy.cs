using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;

namespace ECafe.Domain.Policies;

public static class RestaurantReservationArrivalPolicy
{
    /// <summary>Son gəlişin gününü normallaşdırır; boş limitə gizli müddət əlavə etmir və saatı açıq iş intervalından kənara buraxmır.</summary>
    public static int? NormalizeDayOffset(RestaurantWorkingHour hour)
    {
        if (!hour.LastReservationArrivalAt.HasValue)
        {
            if (hour.LastReservationArrivalDayOffset.HasValue) Invalid();
            return null;
        }
        if (hour.IsClosed) Invalid();
        var offset = hour.LastReservationArrivalDayOffset ?? (hour.LastReservationArrivalAt.Value < hour.OpensAt ? 1 : 0);
        if (offset is < 0 or > 1 || hour.CloseDayOffset is < 0 or > 1) Invalid();
        var arrival = hour.LastReservationArrivalAt.Value.ToTimeSpan() + TimeSpan.FromDays(offset);
        var closeOffset = hour.ClosesAt == TimeOnly.MinValue && hour.OpensAt > hour.ClosesAt ? 1 : hour.CloseDayOffset;
        var end = hour.ClosesAt.ToTimeSpan() + TimeSpan.FromDays(closeOffset);
        if (arrival < hour.OpensAt.ToTimeSpan() || arrival >= end) Invalid();
        return offset;
    }

    /// <summary>Seçilmiş iş gününün başlanğıc tarixinə gün ofsetini əlavə edir; gecəyarısından sonrakı limiti növbəti tarixdə saxlayır.</summary>
    public static DateTimeOffset? ResolveCutoff(RestaurantWorkingHour hour, DateOnly workDate, TimeSpan utcOffset)
        => hour.LastReservationArrivalAt.HasValue
            ? new DateTimeOffset(workDate.AddDays(NormalizeDayOffset(hour)!.Value)
                .ToDateTime(hour.LastReservationArrivalAt.Value), utcOffset)
            : null;

    /// <summary>Yeni rezervasiya üçün qüvvədə olan limiti restoranın saat qurşağında tapır; təyin edilməmiş limit null qalır.</summary>
    public static DateTime? GetCutoffAt(Restaurant restaurant, DateTimeOffset reservedAt)
    {
        var local = RestaurantTimeZoneConverter.ToRestaurantLocalTime(reservedAt, restaurant.TimeZone);
        return RestaurantWorkingHoursCalculator.TryGetActiveInterval(restaurant.WorkingHours, local, out var interval)
            ? interval.ReservationArrivalCutoffAt?.UtcDateTime : null;
    }

    /// <summary>Son təyin olunmuş rezervasiya saatının özünə icazə verir; bundan sonrakı vaxtı bloklayır.</summary>
    public static bool Allows(DateTime arrivalAtUtc, DateTime? cutoffAtUtc)
        => !cutoffAtUtc.HasValue || arrivalAtUtc <= cutoffAtUtc.Value;

    /// <summary>Yanlış limitdə restoranın iş saatları saxlanılmadan əməliyyatı dayandırır.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new BusinessRuleException(ErrorCode.RestaurantReservationArrivalCutoffInvalid);
}
