using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ReservationRefundStatusHistoryEntity = ECafe.Domain.Entities.ReservationRefundStatusHistory;

namespace ECafe.Application.Services.ReservationRefund.Concrete;

internal static class ReservationRefundStatusTransition
{
    public static void Apply(
        ReservationRefundEntity refund,
        int nextStatusId,
        int? changedByUserId,
        DateTime changedAt,
        string reason)
    {
        refund.StatusHistory.Add(new ReservationRefundStatusHistoryEntity
        {
            FromStatusId = refund.StatusId,
            ToStatusId = nextStatusId,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt,
            Reason = reason
        });

        refund.StatusId = nextStatusId;
    }
}
