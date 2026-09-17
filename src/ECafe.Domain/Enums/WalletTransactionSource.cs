namespace ECafe.Domain.Enums
{
    public enum WalletTransactionSource
    {
        Order = 1,
        [Obsolete("Withdrawal requests are no longer part of the wallet flow.")]
        Withdrawal = 2,
        Refund = 3,
        Bonus = 4,
        Commission = 5,
        Adjustment = 6
    }
}
