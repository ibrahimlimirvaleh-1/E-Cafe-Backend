using System.ComponentModel;

namespace ECafe.Domain.Enums;

public enum RefundStatus
{
    [Description("Geri ödəniş sorğusu gözləyir")]
    Requested = 1,

    [Description("Geri ödəniş məlumatları gözlənilir")]
    AwaitingPayoutDetails,

    [Description("Geri ödəniş üçün hazırdır")]
    ReadyForPayout,

    [Description("Geri ödəniş emal edilir")]
    Processing,

    [Description("Geri ödəniş tamamlandı")]
    Refunded,

    [Description("Geri ödənişlə bağlı etiraz var")]
    Disputed,

    [Description("Geri ödəniş uğursuz oldu")]
    Failed,

    [Description("Geri ödəniş sorğusu rədd edildi")]
    Rejected
}
