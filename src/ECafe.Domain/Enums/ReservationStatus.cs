using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum ReservationStatus
    {
        [Description("Ödəniş gözlənilir")]
        PendingPayment = 1,

        [Description("Rezervasiya təsdiqlənib")]
        Confirmed,

        [Description("Müştəri gəlib və stol arxasında əyləşib")]
        Seated,

        [Description("Rezervasiya tamamlanıb")]
        Completed,

        [Description("Rezervasiya ləğv edilib")]
        Cancelled,

        [Description("Müştəri gəlməyib")]
        NoShow,

        [Description("Rezervasiyanın vaxtı bitib")]
        Expired,

        [Description("Ödəniş çeki göndərilib")]
        PaymentSubmitted,

        [Description("Rezervasiya rədd edilib")]
        Rejected
    }


}
