using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies
{
    public static class PaymentMethodPolicy
    {
        public static bool IsMvpSupported(PaymentMethod paymentMethod)
            => paymentMethod == PaymentMethod.Online;

        public static void EnsureMvpSupported(PaymentMethod paymentMethod)
        {
            if (!IsMvpSupported(paymentMethod))
                throw new InvalidOperationException("MVP-də yalnız online ödəniş dəstəklənir.");
        }

        public static IReadOnlyCollection<PaymentMethod> MvpSupportedMethods { get; } =
            new[] { PaymentMethod.Online };
    }
}
