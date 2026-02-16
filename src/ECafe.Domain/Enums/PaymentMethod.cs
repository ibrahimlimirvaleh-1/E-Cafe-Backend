using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum PaymentMethod
    {
        [Description("Nağd ödəniş")]
        Cash = 1,

        [Description("Kartla ödəniş")]
        Card,

        [Description("Onlayn ödəniş")]
        Online
    }

}
