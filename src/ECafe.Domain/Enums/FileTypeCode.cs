using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum FileTypeCode
    {
        [Description("Restoran şəkli")]
        RestaurantImage = 1,

        [Description("Menyu elementi şəkli")]
        MenuItemImage = 2,

        [Description("Profil şəkli")]
        UserProfileImage = 3,

        [Description("Müqavilə sənədi")]
        ContractDocument = 4,

        [Description("Invoice sənədi")]
        InvoiceDocument = 5,

        [Description("Ödəniş qəbzi")]
        PaymentReceipt = 6,

        [Description("Admin sənədi")]
        AdminDocument = 7,

        [Description("Müvəqqəti yükləmə")]
        TemporaryUpload = 8
    }
}
