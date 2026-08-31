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
        public const string ContractExpiryReminderSent = "ContractExpiryReminderSent";

        public const string CategoryCreated = "CategoryCreated";
        public const string CategoryUpdated = "CategoryUpdated";
        public const string CategoryActivated = "CategoryActivated";
        public const string CategoryDeactivated = "CategoryDeactivated";
        public const string CategoryDeleted = "CategoryDeleted";
        public const string ItemCreated = "ItemCreated";
        public const string ItemUpdated = "ItemUpdated";
        public const string ItemDeactivated = "ItemDeactivated";
        public const string ItemDeleted = "ItemDeleted";
        public const string StaffUpdated = "StaffUpdated";
        public const string StaffActivated = "StaffActivated";
        public const string StaffDeactivated = "StaffDeactivated";
        public const string TableUpdated = "TableUpdated";
        public const string TableCopied = "TableCopied";
        public const string TableActivated = "TableActivated";
        public const string TableDeactivated = "TableDeactivated";
        public const string TableDeleted = "TableDeleted";

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
            new(18, ContractExpiryReminderSent, "Müqavilə bitmə xatırlatması göndərildi"),
            new(30, CategoryCreated, "Kateqoriya yaradıldı"),
            new(37, CategoryUpdated, "Kateqoriya yeniləndi"),
            new(41, CategoryActivated, "Kateqoriya aktiv edildi"),
            new(38, CategoryDeactivated, "Kateqoriya deaktiv edildi"),
            new(39, CategoryDeleted, "Kateqoriya silindi"),
            new(31, ItemCreated, "Menyu elementi yaradıldı"),
            new(51, ItemUpdated, "Menyu elementi yeniləndi"),
            new(52, ItemDeactivated, "Menyu elementi deaktiv edildi"),
            new(53, ItemDeleted, "Menyu elementi silindi"),
            new(33, StaffUpdated, "İşçi yeniləndi"),
            new(42, StaffActivated, "İşçi aktiv edildi"),
            new(32, StaffDeactivated, "İşçi deaktiv edildi"),
            new(34, TableUpdated, "Masa yeniləndi"),
            new(44, TableCopied, "Masa kopyalandı"),
            new(43, TableActivated, "Masa aktiv edildi"),
            new(35, TableDeactivated, "Masa deaktiv edildi"),
            new(36, TableDeleted, "Masa silindi"),
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
