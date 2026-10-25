namespace ECafe.Domain.Enums;

public enum OrderCancellationRequestStatus
{
    AwaitingWaiter = 1,
    PendingKitchen = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5
}
