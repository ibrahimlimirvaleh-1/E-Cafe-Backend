namespace ECafe.Application.Common.Audit
{
    public static class AuditActions
    {
        public const string RestaurantCreated = "RestaurantCreated";
        public const string RestaurantUpdated = "RestaurantUpdated";
        public const string RestaurantDeactivated = "RestaurantDeactivated";

        public const string ContractCreated = "ContractCreated";
        public const string ContractActivated = "ContractActivated";
        public const string ContractExpired = "ContractExpired";
        public const string ContractTerminated = "ContractTerminated";

        public const string CategoryCreated = "CategoryCreated";
        public const string ItemCreated = "ItemCreated";

        public const string ReservationCreated = "ReservationCreated";
        public const string OrderCreated = "OrderCreated";
    }
}
