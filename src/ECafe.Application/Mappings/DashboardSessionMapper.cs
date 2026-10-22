using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Application.Mappings;

public static class DashboardSessionMapper
{
    /// <summary>Açıq masa hesabını mövcud billing policy ilə hesablayır; nasaz snapshotu ödənilmiş kimi göstərmək əvəzinə yoxlama üçün işarələyir.</summary>
    public static DashboardSessionRow Map(TableSession session)
    {
        decimal total = 0, remaining = 0;
        bool pending = false, review;
        try
        {
            var bill = TableSessionBillingPolicy.Calculate(session);
            total = bill.TotalAmount; remaining = bill.RemainingAmount; pending = bill.HasPendingPayment; review = bill.RequiresReview;
        }
        catch (BusinessRuleException) { review = true; }
        var preparing = StatusIds.Build(StatusType.Order, (int)OrderStatus.Preparing);
        var ready = StatusIds.Build(StatusType.Order, (int)OrderStatus.Ready);
        return new(session.Id, session.RestaurantId, session.TableId, session.Table.Name ?? session.Table.TableNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            session.WaiterUser == null ? null : $"{session.WaiterUser.Name} {session.WaiterUser.Surname}".Trim(),
            session.ReservationId, session.Reservation?.PeopleCount, session.OpenedAt, total, remaining,
            session.Orders.Count(order => !order.IsDeleted && order.StatusId == preparing),
            session.Orders.Count(order => !order.IsDeleted && order.StatusId == ready), pending, review);
    }
}
