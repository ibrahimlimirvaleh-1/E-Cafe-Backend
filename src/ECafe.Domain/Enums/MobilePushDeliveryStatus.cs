namespace ECafe.Domain.Enums;

public enum MobilePushDeliveryStatus
{
    Pending = 0,
    TicketPending = 1,
    ProviderAccepted = 2,
    Suppressed = 3,
    Failed = 4,
    InvalidToken = 5
}
