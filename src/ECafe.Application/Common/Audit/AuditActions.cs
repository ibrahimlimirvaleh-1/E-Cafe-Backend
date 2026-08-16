namespace ECafe.Application.Common.Audit
{
    public static class AuditActions
    {
        public const string RestaurantCreated = "RestaurantCreated";
        public const string RestaurantUpdated = "RestaurantUpdated";
        public const string RestaurantDeactivated = "RestaurantDeactivated";

        public const string ContractCreated = "ContractCreated";
        public const string ContractUpdated = "ContractUpdated";
        public const string ContractSentForSignature = "ContractSentForSignature";
        public const string ContractOwnerApproved = "ContractOwnerApproved";
        public const string ContractActivated = "ContractActivated";
        public const string ContractScheduled = "ContractScheduled";
        public const string ContractExpired = "ContractExpired";
        public const string ContractTerminated = "ContractTerminated";

        public const string CategoryCreated = "CategoryCreated";
        public const string ItemCreated = "ItemCreated";

        public const string ReservationCreated = "ReservationCreated";
        public const string OrderCreated = "OrderCreated";

        public static IReadOnlyCollection<AuditActionDefinition> All { get; } =
        [
            new(1, RestaurantCreated, "Restoran yaradıldı"),
            new(2, RestaurantUpdated, "Restoran yeniləndi"),
            new(3, RestaurantDeactivated, "Restoran deaktiv edildi"),
            new(10, ContractCreated, "Müqavilə yaradıldı"),
            new(11, ContractUpdated, "Müqavilə yeniləndi"),
            new(12, ContractSentForSignature, "Sahibkar təsdiqinə göndərildi"),
            new(13, ContractOwnerApproved, "Sahibkar təsdiqlədi"),
            new(14, ContractActivated, "Müqavilə aktivləşdirildi"),
            new(15, ContractScheduled, "Müqavilə planlandı"),
            new(16, ContractExpired, "Müqavilənin müddəti bitdi"),
            new(17, ContractTerminated, "Müqavilə ləğv edildi"),
            new(30, CategoryCreated, "Kateqoriya yaradıldı"),
            new(31, ItemCreated, "Menyu elementi yaradıldı"),
            new(40, ReservationCreated, "Rezervasiya yaradıldı"),
            new(50, OrderCreated, "Sifariş yaradıldı")
        ];

        public static string GetDisplayName(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            return All.FirstOrDefault(x => x.Code == action.Trim())?.DisplayName ?? action.Trim();
        }
    }

    public sealed record AuditActionDefinition(int Id, string Code, string DisplayName);
}
