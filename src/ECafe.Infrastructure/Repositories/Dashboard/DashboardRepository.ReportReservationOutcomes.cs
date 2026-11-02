using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using View = ECafe.Application.Mappings.DashboardReportDetailMapper;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Rezervasiyanın planlı yerli saatı üzrə nəticə və gecikməni aqreqasiya edir; texniki yaradılma tarixini gəliş tarixi kimi istifadə etmir.</summary>
    private async Task<DashboardReportData> ReadReservationOutcomeReportAsync(DashboardScope scope, Domain.Policies.DashboardPeriod period, int pageIndex, bool export, CancellationToken token)
    {
        var ids = Restaurants(scope).Select(row => row.Id);
        var cancelled = StatusIds.Reservation(ReservationStatus.Cancelled);
        var noShow = StatusIds.Reservation(ReservationStatus.NoShow);
        var confirmed = StatusIds.Reservation(ReservationStatus.Confirmed);
        var seated = StatusIds.Reservation(ReservationStatus.Seated);
        var completed = StatusIds.Reservation(ReservationStatus.Completed);
        var query = db.Reservations.AsNoTracking().Where(r => ids.Contains(r.RestaurantId) && r.ReservedAt >= period.StartUtc && r.ReservedAt < period.EndUtc)
            .GroupBy(r => new { r.RestaurantId, Restaurant = r.Restaurant.Name,
                Day = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(r.ReservedAt, period.TimeZone).DayOfWeek,
                Hour = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(r.ReservedAt, period.TimeZone).Hour })
            .Select(group => new { group.Key.RestaurantId, group.Key.Restaurant, group.Key.Day, group.Key.Hour, Count = group.Count(), Guests = group.Sum(r => r.PeopleCount),
                Confirmed = group.Count(r => r.StatusId == confirmed || r.StatusId == seated || r.StatusId == completed),
                Cancelled = group.Count(r => r.StatusId == cancelled), NoShow = group.Count(r => r.StatusId == noShow),
                Arrived = group.Count(r => r.ArrivedAt != null), Late = group.Count(r => r.ArrivedAt > r.ReservedAt),
                Seated = group.Count(r => r.SeatedAt != null) });
        var count = await query.CountAsync(token);
        var page = ReportPage(count, pageIndex, export);
        var stats = await query.GroupBy(_ => 1).Select(group => new { Count = group.Sum(row => row.Count), NoShow = group.Sum(row => row.NoShow), Cancelled = group.Sum(row => row.Cancelled) }).SingleOrDefaultAsync(token);
        var rows = await query.OrderByDescending(row => row.Count).ThenBy(row => row.RestaurantId).ThenBy(row => row.Day).ThenBy(row => row.Hour).Skip(page.Skip).Take(page.Take).ToListAsync(token);
        return View.Map(count, "restaurant arrivalDay:code arrivalHour reservations:number guests:number confirmed:number arrived:number late:number seated:number cancelled:number noShow:number noShowRate:percent", rows.Select(row =>
            View.Row($"{row.RestaurantId}:{row.Day}:{row.Hour}", row.Restaurant, "day." + (int)row.Day, $"{row.Hour:00}:00", row.Count, row.Guests, row.Confirmed, row.Arrived, row.Late, row.Seated, row.Cancelled, row.NoShow,
                decimal.Round(100m * row.NoShow / row.Count, 2))),
            View.Metric("reservations", stats?.Count ?? 0), View.Metric("cancelledReservations", stats?.Cancelled ?? 0), View.Metric("noShow", stats?.NoShow ?? 0));
    }
}
