using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies
{
    public static class PaymentMethodPolicy
    {
        public static bool IsMvpSupported(PaymentMethod paymentMethod)
            => paymentMethod is PaymentMethod.Online or PaymentMethod.BankTransfer;

        public static void EnsureMvpSupported(PaymentMethod paymentMethod)
        {
            if (!IsMvpSupported(paymentMethod))
                throw new InvalidOperationException("MVP-də yalnız online və köçürmə ilə ödəniş dəstəklənir.");
        }

        public static IReadOnlyCollection<PaymentMethod> MvpSupportedMethods { get; } =
            new[] { PaymentMethod.Online, PaymentMethod.BankTransfer };
    }
}
