using ECafe.Application.DTOs.TableSession;
using ECafe.Domain.Entities;

namespace ECafe.Application.Services.TableSessions.Concrete;

public static class TableSessionViewMapper
{
    /// <summary>Sessiyanı maliyyə və əlaqə məlumatları olmayan xidmət görünüşünə, işgünü sonu yoxlama göstəricisi ilə çevirir.</summary>
    public static WaiterTableSessionResponse Map(TableSession session, DateTime nowUtc) => new()
    {
        Id = session.Id, RestaurantId = session.RestaurantId, TableId = session.TableId,
        TableNo = session.Table.TableNo, TableName = session.Table.Name, TableCapacity = session.Table.Capacity,
        StatusId = session.StatusId, ReservationId = session.ReservationId,
        CustomerName = session.CustomerUser == null ? null : (session.CustomerUser.Name + " " + session.CustomerUser.Surname).Trim(),
        PeopleCount = session.Reservation?.PeopleCount, OpenedAt = Offset(session.OpenedAt)!.Value,
        MustVacateAt = Offset(session.Reservation?.MustVacateAt), TimeZone = session.Restaurant.TimeZone,
        CloseReviewDueAt = Offset(TableSessionCloseReviewPolicy.GetDueAt(session)),
        RequiresCloseReview = TableSessionCloseReviewPolicy.RequiresReview(session, nowUtc),
        CloseReviewConfirmedAt = Offset(session.CloseReviewConfirmedAt)
    };

    /// <summary>Bazada UTC saxlanan nullable tarixi API-nin UTC offset formatına çevirir.</summary>
    public static DateTimeOffset? Offset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
