using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies
{
    public static class PaymentMethodPolicy
    {
        /// <summary>Yeni ödənişdə yalnız provayderin onlayn axınına icazə verir; əl ilə köçürmə artıq qəbul edilmir.</summary>
        public static bool IsMvpSupported(PaymentMethod paymentMethod)
            => paymentMethod == PaymentMethod.Online;

        /// <summary>Köhnə əl ilə köçürmə üsulunun yeni əməliyyatda seçilməsini dayandırır.</summary>
        public static void EnsureMvpSupported(PaymentMethod paymentMethod)
        {
            if (!IsMvpSupported(paymentMethod))
                throw new InvalidOperationException("Yeni ödəniş yalnız onlayn provayder üzərindən aparılır.");
        }

        public static IReadOnlyCollection<PaymentMethod> MvpSupportedMethods { get; } =
            new[] { PaymentMethod.Online };
    }
}
