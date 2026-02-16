using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum PermissionCode
    {
        [Description("İstifadəçiləri idarə etmək")]
        ManageUsers = 1,

        [Description("Restoranları idarə etmək")]
        ManageRestaurants,

        [Description("Kateqoriya və məhsulları idarə etmək")]
        ManageCatalog,

        [Description("Stolları idarə etmək")]
        ManageTables,

        [Description("Rezervasiyaları idarə etmək")]
        ManageReservations,

        [Description("Sifarişləri idarə etmək")]
        ManageOrders,

        [Description("Ödənişləri idarə etmək")]
        ManagePayments,

        [Description("Hesabatlara baxmaq")]
        ViewReports
    }

}
