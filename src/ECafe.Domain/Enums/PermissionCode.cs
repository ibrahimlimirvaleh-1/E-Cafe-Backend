using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum PermissionCode
    {
        [Description("İstifadəçiləri idarə etmək")]
        ManageUsers = 1,

        [Description("İşçiləri idarə etmək")]
        ManageStaff,

        [Description("Rolları təyin etmək")]
        AssignRoles,

        [Description("Restoranları idarə etmək")]
        ManageRestaurants,

        [Description("Kateqoriyaları və məhsulları idarə etmək")]
        ManageCatalog,

        [Description("Stolları idarə etmək")]
        ManageTables,

        [Description("Rezervasiyaları idarə etmək")]
        ManageReservations,

        [Description("Təyin olunmuş rezervasiyalara baxmaq")]
        ViewAssignedReservations,

        [Description("Sifarişləri idarə etmək")]
        ManageOrders,

        [Description("Ödənişləri idarə etmək")]
        ManagePayments,

        [Description("Rəyləri idarə etmək")]
        ManageReviews,

        [Description("Balansa nəzarət etmək")]
        ManageWallet,

        [Description("Öz balansına baxmaq")]
        ViewOwnWallet,

        [Description("Çıxarış sorğularını idarə etmək")]
        ManageWithdrawRequests,

        [Description("Hesabatlara baxmaq")]
        ViewReports,

        [Description("Dashboard-a baxmaq")]
        ViewDashboard,

        [Description("Audit qeydlərinə baxmaq")]
        ViewAuditLogs
    }
}