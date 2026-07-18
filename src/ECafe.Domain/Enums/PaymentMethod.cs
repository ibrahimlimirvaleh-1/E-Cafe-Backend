using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum PaymentMethod
    {
        [Obsolete("MVP-də nağd ödəniş deaktivdir. PaymentMethodPolicy.IsMvpSupported ilə yoxla.")]
        [Description("Nağd ödəniş")]
        Cash = 1,

        [Obsolete("MVP-də restoran POS/kart ödənişi sistem payment source-of-truth sayılmır. Online istifadə et.")]
        [Description("Kartla ödəniş")]
        Card = 2,

        [Description("Onlayn ödəniş")]
        Online = 3
    }
}
