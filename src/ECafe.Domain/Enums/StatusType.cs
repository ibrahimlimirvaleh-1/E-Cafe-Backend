using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum StatusType
    {
        [Description("Rezervasiya statusları")]
        Reservation = 1,

        [Description("Sifariş statusları")]
        Order,

        [Description("Ödəniş statusları")]
        PaymentStatus,

        [Description("Ödəniş üsulları")]
        PaymentMethod
    }


}
