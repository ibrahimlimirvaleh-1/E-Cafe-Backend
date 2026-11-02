using System.Linq.Expressions;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Domain.Policies;

public static class CustomerOrderSubmissionPolicy
{
    /// <summary>Müştəri sifarişini yalnız aktiv restoranın, müqavilənin və ofisiant təyinatının açıq sessiyasında mümkün edir; davam edən hesablaşmanı qoruyur.</summary>
    public static Expression<Func<TableSession, bool>> AvailableAt(DateTime nowUtc) => session =>
        !session.IsDeleted && session.ClosedAt == null &&
        session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) &&
        session.CustomerUser != null && session.CustomerUser.IsActive && !session.CustomerUser.IsDeleted &&
        session.Restaurant.IsActive && !session.Restaurant.IsDeleted &&
        session.Table.IsActive && !session.Table.IsDeleted && session.Table.RestaurantId == session.RestaurantId &&
        session.Restaurant.Contracts.Any(contract => !contract.IsDeleted &&
            contract.StatusId == StatusIds.Contract(ContractStatus.Active) &&
            (!contract.EndDate.HasValue || contract.EndDate >= nowUtc)) &&
        session.WaiterUser != null && session.WaiterUser.IsActive && !session.WaiterUser.IsDeleted &&
        session.WaiterUser.UserRestaurants.Any(assignment => !assignment.IsDeleted && assignment.IsActive &&
            assignment.RestaurantId == session.RestaurantId && assignment.RoleId == (int)RoleCode.Waiter) &&
        !session.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) || payment.RequiresRefund));

    /// <summary>Müştərinin gözləyən sifarişinin yalnız ilk təsdiq üçün uyğun mərhələdə olduğunu hesablayır; rol və sessiya xidmətdə təkrar yoxlanır.</summary>
    public static bool CanApprove(Order order) => !order.IsDeleted && order.SourceId == (int)OrderSource.Customer &&
        order.StatusId == StatusIds.Build(StatusType.Order, (int)OrderStatus.PendingWaiterConfirmation) &&
        order.SentToKitchenAt == null && order.AcceptedAt == null && order.PreparingAt == null &&
        order.ReadyAt == null && order.ServedAt == null && order.ClosedAt == null && order.CancelledAt == null &&
        order.OrderItems.Any(line => !line.IsDeleted && line.Quantity > 0) &&
        !order.OrderItems.Any(line => !line.IsDeleted && line.Quantity <= 0) &&
        !order.Payments.Any(payment => !payment.IsDeleted &&
            (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) ||
             payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid) || payment.RequiresRefund));
}
