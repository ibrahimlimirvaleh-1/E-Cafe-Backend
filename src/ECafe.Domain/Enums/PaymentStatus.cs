using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum PaymentStatus
    {
        [Description("Ödəniş gözlənilir")]
        Pending = 1,

        [Description("Ödəniş uğurla tamamlanıb")]
        Paid,

        [Description("Ödəniş uğursuz olub")]
        Failed,

        [Description("Ödəniş ləğv edilib")]
        Cancelled,

        [Description("Ödəniş geri qaytarılıb")]
        Refunded,

        [Description("Ödəniş sübutu göndərilib")]
        Submitted,

        [Description("Ödəniş rədd edilib")]
        Rejected
    }

}
    
