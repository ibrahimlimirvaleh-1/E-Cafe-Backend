using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum ReservationStatus
    {
        [Description("Depozit ödənişi gözlənilir")]
        PendingDeposit = 1,

        [Description("Depozit ödənilib, stol rezerv olunub")]
        Reserved,

        [Description("Müştəri gəlib və stol arxasında əyləşib")]
        Seated,

        [Description("Rezervasiya tamamlanıb")]
        Completed,

        [Description("Rezervasiya ləğv edilib")]
        Cancelled,

        [Description("Müştəri gəlməyib")]
        NoShow,

        [Description("Rezervasiyanın vaxtı bitib")]
        Expired
    }


}
