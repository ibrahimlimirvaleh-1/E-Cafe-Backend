namespace ECafe.Domain.Enums
{
    public enum NotificationType
    {
        ContractPendingApproval = 1,
        ContractOwnerApproved = 2,
        ContractActivated = 3,
        ContractTerminated = 4,
        ContractExpired = 5,
        ReservationCreated = 6,
        OrderCreated = 7,
        OrderReady = 8,
        InventoryLowStock = 9,
        ContractExpiryReminder = 10
    }
}
