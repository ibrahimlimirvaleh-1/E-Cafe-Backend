using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;

namespace ECafe.Application.Services.TableSessions.Concrete;

public static class TableSessionCloseReviewPolicy
{
    /// <summary>Yeni və köhnə sessiyanın açıldığı iş intervalının sonunu tapır; gecəyarısını və DST dəyişməsini restoranın yerli vaxtı ilə hesablayır.</summary>
    public static DateTime GetDueAt(TableSession session)
    {
        if (session.CloseReviewDueAt is { } dueAt) return dueAt;
        var restaurant = session.Restaurant;
        var opened = new DateTimeOffset(DateTime.SpecifyKind(session.OpenedAt, DateTimeKind.Utc));
        var local = RestaurantTimeZoneConverter.ToRestaurantLocalTime(opened, restaurant?.TimeZone);
        if (restaurant is not null && RestaurantWorkingHoursCalculator.TryGetActiveInterval(
                restaurant.WorkingHours.Where(hour => !hour.IsDeleted), local, out var interval))
        {
            try { return RestaurantTimeZoneConverter.ToUtc(interval.EndsAt.DateTime, restaurant.TimeZone); }
            catch (ArgumentException) { /* Etibarsız cədvəl sessiyanı bağlamır, əl ilə yoxlamaya yönəldir. */ }
        }
        return DateTime.SpecifyKind(session.OpenedAt, DateTimeKind.Utc);
    }

    /// <summary>Bağlanmamış sessiyanın işgünü və ya menecerin seçdiyi növbəti baxış vaxtı bitdikdə əlavə yoxlama tələb edir.</summary>
    public static bool RequiresReview(TableSession session, DateTime nowUtc)
        => !session.IsDeleted && session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) &&
           session.ClosedAt == null && GetDueAt(session) <= nowUtc;
}
